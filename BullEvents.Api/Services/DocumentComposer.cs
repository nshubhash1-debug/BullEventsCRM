using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>
/// Fills a template with one booking's actual numbers.
///
/// The merge is deliberately dumb: tokens in, values out, no expressions, no
/// conditionals, no loops an author can write. A template language rich enough
/// to be interesting is rich enough to be a scripting engine, and this one
/// renders documents that go to customers and carry money figures — the failure
/// mode of a clever template is a demand letter for the wrong amount, which is
/// worse than a template that cannot express something.
///
/// The three table tokens are the exception, and they are rendered by this code
/// rather than composed by the author for exactly that reason.
///
/// Every substituted value is HTML-encoded. A customer whose address contains
/// an ampersand should not be able to break the letter, and a name is untrusted
/// input like any other.
/// </summary>
public class DocumentComposer(AppDbContext db)
{
    private static readonly Regex Token = new(@"\{\{\s*([a-zA-Z]+)\.?([a-zA-Z]*)\s*\}\}", RegexOptions.Compiled);

    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");

    /// <summary>What a render needs to know beyond the booking itself.</summary>
    public record Context(int BookingId, int? DemandId = null, int? ReceiptId = null);

    /// <summary>
    /// Renders a template body against a booking.
    ///
    /// Returns the filled text plus the tokens that could not be resolved, so a
    /// preview can say "this letter has three blanks in it" rather than quietly
    /// producing a letter with holes.
    /// </summary>
    public async Task<(string Body, IReadOnlyList<string> Unresolved)> RenderAsync(
        string template, Context context, CancellationToken ct = default)
    {
        var values = await ValuesAsync(context, ct);
        var unresolved = new List<string>();

        var rendered = Token.Replace(template, match =>
        {
            var key = string.IsNullOrEmpty(match.Groups[2].Value)
                ? match.Groups[1].Value
                : $"{match.Groups[1].Value}.{match.Groups[2].Value}";

            if (values.TryGetValue(key, out var value)) return value;

            unresolved.Add(match.Value);

            // Left as the token rather than blanked. A visible {{demand.amount}}
            // in a preview is a bug somebody fixes; an empty space is one that
            // ships.
            return match.Value;
        });

        return (rendered, unresolved.Distinct().ToList());
    }

    /// <summary>Every token this context can resolve, already encoded for HTML.</summary>
    public async Task<Dictionary<string, string>> ValuesAsync(
        Context context, CancellationToken ct = default)
    {
        var booking = await db.Bookings
            .Include(b => b.Applicants)
            .FirstOrDefaultAsync(b => b.Id == context.BookingId, ct)
            ?? throw ApiException.NotFound("Booking");

        var company = await db.Companies
            .Where(c => c.Id == booking.CompanyId)
            .Select(c => new { c.Name })
            .FirstOrDefaultAsync(ct);

        var primary = booking.Applicants
            .OrderBy(a => a.SortOrder)
            .FirstOrDefault(a => a.Role == ApplicantRoles.Primary)
            ?? booking.Applicants.FirstOrDefault();

        var others = booking.Applicants
            .Where(a => a.Role != ApplicantRoles.Primary)
            .Select(a => a.Name)
            .ToList();

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["company.name"] = Safe(company?.Name),
            // The company row carries no postal address today, so the token
            // renders as a dash rather than as an empty line in a letterhead.
            ["company.address"] = Safe(null),

            ["booking.number"] = Safe(booking.BookingNumber),
            ["booking.date"] = Date(booking.BookingDate),
            ["booking.status"] = Safe(booking.Status),
            ["booking.agreementValue"] = Money(booking.AgreementValue),
            ["booking.grandTotal"] = Money(booking.GrandTotal),
            ["booking.received"] = Money(booking.Received),
            ["booking.outstanding"] = Money(booking.Outstanding),

            ["customer.name"] = Safe(primary?.Name),
            ["customer.salutation"] = Safe(primary?.Salutation),
            ["customer.address"] = Safe(primary?.Address),
            ["customer.phone"] = Safe(primary?.Phone),
            ["customer.email"] = Safe(primary?.Email),
            ["customer.pan"] = Safe(primary?.Pan),
            ["customer.coApplicants"] = Safe(others.Count == 0 ? "—" : string.Join(", ", others)),

            ["unit.number"] = Safe(booking.UnitNumber),
            ["unit.tower"] = Safe(booking.TowerName),
            ["unit.project"] = Safe(booking.ProjectName),
            ["unit.configuration"] = Safe(booking.Configuration),
            ["unit.area"] = $"{booking.SaleableArea:N0} sq.ft.",

            ["today"] = Date(DateTime.UtcNow),
        };

        /* ---------------- the demand, when there is one ---------------- */

        if (context.DemandId is int demandId)
        {
            var demand = await db.Demands.FirstOrDefaultAsync(d => d.Id == demandId, ct);

            if (demand is not null)
            {
                var late = (int)(DateTime.UtcNow.Date - demand.DueDate.Date).TotalDays;

                values["demand.number"] = Safe(demand.DemandNumber);
                values["demand.label"] = Safe(demand.Label);
                values["demand.amount"] = Money(demand.TotalAmount);
                values["demand.dueDate"] = Date(demand.DueDate);
                values["demand.outstanding"] = Money(demand.Outstanding);
                values["demand.interest"] = Money(demand.InterestDue);
                values["demand.interestRate"] = $"{demand.InterestRatePercent:0.##}% p.a.";
                values["demand.daysLate"] = Math.Max(0, late).ToString();
            }
        }

        /* ---------------- the receipt, when there is one ---------------- */

        if (context.ReceiptId is int receiptId)
        {
            var receipt = await db.Receipts.FirstOrDefaultAsync(r => r.Id == receiptId, ct);

            if (receipt is not null)
            {
                values["receipt.number"] = Safe(receipt.ReceiptNumber);
                values["receipt.amount"] = Money(receipt.CreditedAmount);
                values["receipt.date"] = Date(receipt.ReceivedOn);
                values["receipt.mode"] = Safe(receipt.Mode);
                values["receipt.tds"] = Money(receipt.TdsAmount);
            }
        }

        /* ---------------- the tables ---------------- */

        values["table.schedule"] = await ScheduleTableAsync(booking.Id, ct);
        values["table.outstanding"] = await OutstandingTableAsync(booking.Id, ct);
        values["table.ledger"] = await LedgerTableAsync(booking.Id, ct);

        return values;
    }

    /* ------------------------------------------------------------------ *
     * Tables
     *
     * Rendered with inline styles rather than classes. These end up in an
     * email client and in a print dialog, neither of which loads the app's
     * stylesheet, and a table with no borders in Outlook is not a schedule.
     * ------------------------------------------------------------------ */

    private const string TableOpen =
        "<table style=\"width:100%;border-collapse:collapse;font-size:13px;margin:8px 0\">";

    private const string Th =
        "style=\"text-align:left;padding:6px 8px;border-bottom:2px solid #ddd;font-weight:600\"";

    private const string Td = "style=\"padding:6px 8px;border-bottom:1px solid #eee\"";
    private const string TdRight = "style=\"padding:6px 8px;border-bottom:1px solid #eee;text-align:right\"";

    private async Task<string> ScheduleTableAsync(int bookingId, CancellationToken ct)
    {
        var rows = await db.BookingMilestones
            .Where(m => m.BookingId == bookingId)
            .OrderBy(m => m.SortOrder)
            .ToListAsync(ct);

        if (rows.Count == 0) return "<p>No payment schedule.</p>";

        var html = new StringBuilder(TableOpen);
        html.Append($"<tr><th {Th}>#</th><th {Th}>Instalment</th><th {Th}>Due</th>"
            + $"<th {Th} style=\"text-align:right\">Amount</th><th {Th}>Status</th></tr>");

        foreach (var row in rows)
        {
            html.Append($"<tr><td {Td}>{row.SortOrder}</td>"
                + $"<td {Td}>{Safe(row.Label)}</td>"
                + $"<td {Td}>{(row.DueDate is null ? "—" : Date(row.DueDate.Value))}</td>"
                + $"<td {TdRight}>{Money(row.TotalAmount)}</td>"
                + $"<td {Td}>{Safe(row.Status)}</td></tr>");
        }

        html.Append($"<tr><td {Td} colspan=\"3\"><strong>Total</strong></td>"
            + $"<td {TdRight}><strong>{Money(rows.Where(r => r.Status != MilestoneStatuses.Waived).Sum(r => r.TotalAmount))}</strong></td>"
            + $"<td {Td}></td></tr>");

        return html.Append("</table>").ToString();
    }

    private async Task<string> OutstandingTableAsync(int bookingId, CancellationToken ct)
    {
        var rows = await db.Demands
            .Where(d => d.BookingId == bookingId && d.Status != DemandStatuses.Cancelled)
            .OrderBy(d => d.DueDate)
            .ToListAsync(ct);

        var open = rows.Where(d => d.Outstanding > 0.5m).ToList();

        if (open.Count == 0) return "<p>Nothing is outstanding.</p>";

        var html = new StringBuilder(TableOpen);
        html.Append($"<tr><th {Th}>Demand</th><th {Th}>For</th><th {Th}>Due</th>"
            + $"<th {Th} style=\"text-align:right\">Outstanding</th>"
            + $"<th {Th} style=\"text-align:right\">Interest</th></tr>");

        foreach (var row in open)
        {
            html.Append($"<tr><td {Td}>{Safe(row.DemandNumber)}</td>"
                + $"<td {Td}>{Safe(row.Label)}</td>"
                + $"<td {Td}>{Date(row.DueDate)}</td>"
                + $"<td {TdRight}>{Money(row.Outstanding)}</td>"
                + $"<td {TdRight}>{Money(row.InterestDue)}</td></tr>");
        }

        html.Append($"<tr><td {Td} colspan=\"3\"><strong>Total due</strong></td>"
            + $"<td {TdRight}><strong>{Money(open.Sum(d => d.Outstanding))}</strong></td>"
            + $"<td {TdRight}><strong>{Money(open.Sum(d => d.InterestDue))}</strong></td></tr>");

        return html.Append("</table>").ToString();
    }

    private async Task<string> LedgerTableAsync(int bookingId, CancellationToken ct)
    {
        var demands = await db.Demands
            .Where(d => d.BookingId == bookingId && d.Status != DemandStatuses.Cancelled)
            .ToListAsync(ct);

        var receipts = await db.Receipts
            .Where(r => r.BookingId == bookingId && r.Status == ReceiptStatuses.Cleared)
            .ToListAsync(ct);

        var lines = demands
            .Select(d => (On: d.RaisedOn, What: d.Label, Ref: d.DemandNumber,
                Debit: d.TotalAmount, Credit: 0m))
            .Concat(receipts.Select(r => (On: r.ReceivedOn, What: $"{r.Mode} received",
                Ref: r.ReceiptNumber, Debit: 0m, Credit: r.CreditedAmount)))
            .OrderBy(l => l.On)
            .ToList();

        if (lines.Count == 0) return "<p>Nothing on the account yet.</p>";

        var html = new StringBuilder(TableOpen);
        html.Append($"<tr><th {Th}>Date</th><th {Th}>Particulars</th><th {Th}>Reference</th>"
            + $"<th {Th} style=\"text-align:right\">Debit</th>"
            + $"<th {Th} style=\"text-align:right\">Credit</th>"
            + $"<th {Th} style=\"text-align:right\">Balance</th></tr>");

        var balance = 0m;

        foreach (var line in lines)
        {
            balance += line.Debit - line.Credit;

            html.Append($"<tr><td {Td}>{Date(line.On)}</td>"
                + $"<td {Td}>{Safe(line.What)}</td>"
                + $"<td {Td}>{Safe(line.Ref)}</td>"
                + $"<td {TdRight}>{(line.Debit > 0 ? Money(line.Debit) : "—")}</td>"
                + $"<td {TdRight}>{(line.Credit > 0 ? Money(line.Credit) : "—")}</td>"
                + $"<td {TdRight}>{Money(balance)}</td></tr>");
        }

        return html.Append("</table>").ToString();
    }

    /* ---------------- formatting ---------------- */

    /// <summary>
    /// Indian grouping, because ₹1,92,74,700 read as ₹19,274,700 is out by an
    /// order of magnitude for a beat — and this is a letter about money going to
    /// somebody who will check it.
    /// </summary>
    private static string Money(decimal value) => value.ToString("C0", India);

    private static string Date(DateTime value) => value.ToString("d MMM yyyy", India);

    /// <summary>HTML-encoded, always. A name is untrusted input like any other.</summary>
    private static string Safe(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "—" : WebUtility.HtmlEncode(value);
}
