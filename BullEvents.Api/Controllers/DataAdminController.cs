using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using BullEvents.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/* ------------------------------------------------------------------ *
 * DTOs
 * ------------------------------------------------------------------ */

/// <summary>One field an import can be pointed at, and whether it must be filled.</summary>
public record ImportFieldDto(string Name, string Label, bool Required, string? Note);

public record ImportRowRequest(Dictionary<string, string?> Values);

public record ImportRequest(
    [Required] string Object,
    int BranchId,
    /// <summary>Skip a row whose phone already exists rather than failing the batch.</summary>
    bool SkipDuplicates,
    /// <summary>Let the assignment rules route each row, rather than leaving them unowned.</summary>
    bool ApplyAssignmentRules,
    IReadOnlyList<ImportRowRequest> Rows);

public record ImportProblemDto(int Row, string Reason);

public record ImportResultDto(
    int Received,
    int Created,
    int Skipped,
    int Failed,
    IReadOnlyList<ImportProblemDto> Problems);

public record MassTransferRequest(
    [Required] string Object,
    /// <summary>Whose records move. Null means the unowned ones.</summary>
    int? FromUserId,
    [Required] int ToUserId,
    string? Stage,
    string? Source,
    string? City,
    /// <summary>Report what would move without moving it.</summary>
    bool Preview);

public record MassTransferResultDto(
    int Matched,
    int Moved,
    bool WasPreview,
    IReadOnlyList<string> Sample);

/* ------------------------------------------------------------------ *
 * Controller
 * ------------------------------------------------------------------ */

/// <summary>
/// Moving data in and out in bulk, and moving ownership in bulk.
///
/// All three are administrator tools that touch many records at once, which is
/// why they are gated on the same "act on other people's records" power as the
/// access console rather than on ordinary object permissions. A rep who may
/// edit their own leads has no business reassigning four hundred of somebody
/// else's.
/// </summary>
[ApiController]
[Route("api/admin/data")]
[Authorize]
[RequirePermission(SecuredObjects.User, ObjectAction.ModifyAll)]
public class DataAdminController(
    AppDbContext db,
    AssignmentRuleEngine assignment,
    DuplicateRuleChecker duplicates) : ControllerBase
{
    /* ---------------- what can be imported ---------------- */

    /// <summary>
    /// The fields an import may map onto.
    ///
    /// A short, explicit list rather than every column on the entity. An import
    /// screen that offers three hundred fields is one nobody can map correctly,
    /// and half of those columns are computed anyway.
    /// </summary>
    [HttpGet("import/fields")]
    public ActionResult<IReadOnlyList<ImportFieldDto>> ImportFields([FromQuery] string? o = null)
        => Ok(LeadImportFields);

    private static readonly ImportFieldDto[] LeadImportFields =
    [
        new("Name", "Full name", true, null),
        new("Phone", "Phone", true, "Any format — separators and country codes are stripped"),
        new("Email", "Email", false, null),
        new("City", "City", false, null),
        new("State", "State", false, null),
        new("Source", "Source", false, "Falls back to the import default"),
        new("EventType", "Event type", false, "Wedding, Reception, Birthday, Conference"),
        new("EventDate", "Event date", false, "Day first — 04/11/2026 is 4 November"),
        new("GuestCount", "Guest count", false, "Numbers only"),
        new("ServicesNeeded", "Services needed", false, "Comma separated — Venue,Catering,Decor"),
        new("BudgetMin", "Budget from", false, "Numbers only"),
        new("BudgetMax", "Budget to", false, "Numbers only"),
        new("Notes", "Notes", false, null),
    ];

    /* ---------------- import ---------------- */

    /// <summary>
    /// Creates records from mapped rows.
    ///
    /// Rows are validated and inserted one at a time rather than as a single
    /// transaction. A four-hundred-row file with two bad phone numbers should
    /// import three hundred and ninety-eight and tell you about the two — not
    /// refuse the lot, which is what turns a five-minute job into an afternoon
    /// of bisecting a spreadsheet.
    /// </summary>
    [HttpPost("import")]
    public async Task<ActionResult<ImportResultDto>> Import(
        ImportRequest input, CancellationToken ct)
    {
        if (input.Rows.Count == 0)
        {
            return BadRequest(new { message = "There are no rows to import." });
        }

        if (input.Rows.Count > 5000)
        {
            return BadRequest(new
            {
                message = "Import at most 5,000 rows at a time so a failure is recoverable.",
            });
        }

        var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == input.BranchId, ct)
            ?? await db.Branches.FirstOrDefaultAsync(ct);

        if (branch is null) return BadRequest(new { message = "This company has no branch to import into." });

        var problems = new List<ImportProblemDto>();
        var created = 0;
        var skipped = 0;

        for (var index = 0; index < input.Rows.Count; index++)
        {
            var values = input.Rows[index].Values;
            var line = index + 2; // The header is row one in the file the user is looking at.

            var name = Text(values, "Name");
            var phone = Text(values, "Phone");

            if (string.IsNullOrWhiteSpace(name))
            {
                problems.Add(new ImportProblemDto(line, "No name."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(phone))
            {
                problems.Add(new ImportProblemDto(line, "No phone number."));
                continue;
            }

            if (input.SkipDuplicates)
            {
                var verdict = await duplicates.CheckLeadAsync(
                    name, phone, Text(values, "Email"), ct: ct);

                if (verdict is not null && verdict.Matches.Count > 0)
                {
                    skipped++;
                    continue;
                }
            }

            var lead = new Lead
            {
                CompanyId = branch.CompanyId,
                BranchId = branch.Id,
                Stage = LeadStages.New,
                Name = name.Trim(),
                Phone = phone.Trim(),
                Email = Text(values, "Email"),
                City = Text(values, "City"),
                State = Text(values, "State"),
                Source = Text(values, "Source") ?? LeadSources.Other,
                EventType = Text(values, "EventType"),
                EventCategory = Text(values, "EventType") is { } occasion
                    ? EventTypes.CategoryOf(occasion)
                    : null,
                EventDate = Date(values, "EventDate"),
                GuestCount = Whole(values, "GuestCount"),
                ServicesNeeded = Text(values, "ServicesNeeded"),
                BudgetMin = Money(values, "BudgetMin"),
                BudgetMax = Money(values, "BudgetMax"),
                Notes = Text(values, "Notes"),
            };

            if (input.ApplyAssignmentRules)
            {
                lead.OwnerId = await assignment.ResolveOwnerAsync(SecuredObjects.Lead, lead, ct);
            }

            db.Leads.Add(lead);

            try
            {
                await db.SaveChangesAsync(ct);
                created++;
            }
            catch (Exception error)
            {
                // Detach the failed row so the next SaveChanges is not still
                // carrying it — otherwise one bad row fails every row after it.
                db.Entry(lead).State = EntityState.Detached;
                problems.Add(new ImportProblemDto(line, Root(error)));
            }
        }

        return Ok(new ImportResultDto(
            input.Rows.Count, created, skipped, problems.Count,
            problems.Take(50).ToList()));
    }

    /* ---------------- export ---------------- */

    /// <summary>
    /// Streams the leads this user may see as a CSV.
    ///
    /// Scoped, deliberately: an export is the easiest way to walk out of a
    /// business with its book, and one that ignored record visibility would let
    /// anybody who can reach this endpoint take everything.
    /// </summary>
    [HttpGet("export/leads")]
    public async Task<IActionResult> ExportLeads(
        [FromServices] AccessScope scope,
        [FromQuery] string? stage = null,
        [FromQuery] int? ownerId = null,
        CancellationToken ct = default)
    {
        var query = db.Leads.Include(l => l.Owner).Include(l => l.Branch).AsQueryable();

        query = await scope.ApplyAsync(query, ct);

        if (!string.IsNullOrWhiteSpace(stage)) query = query.Where(l => l.Stage == stage);
        if (ownerId is int owner) query = query.Where(l => l.OwnerId == owner);

        var rows = await query
            .OrderByDescending(l => l.CreatedAt)
            .Take(50_000)
            .ToListAsync(ct);

        var csv = new StringBuilder();

        csv.AppendLine(string.Join(',', new[]
        {
            "Name", "Phone", "Email", "City", "State", "Source", "Stage",
            "Priority", "EventType", "EventDate", "GuestCount", "ServicesNeeded",
            "BudgetMin", "BudgetMax", "Owner", "Branch", "Created",
        }));

        foreach (var lead in rows)
        {
            csv.AppendLine(string.Join(',', new[]
            {
                Csv(lead.Name), Csv(lead.Phone), Csv(lead.Email), Csv(lead.City),
                Csv(lead.State), Csv(lead.Source), Csv(lead.Stage), Csv(lead.Priority),
                Csv(lead.EventType),
                Csv(lead.EventDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                Csv(lead.GuestCount?.ToString(CultureInfo.InvariantCulture)),
                Csv(lead.ServicesNeeded),
                Csv(lead.BudgetMin?.ToString(CultureInfo.InvariantCulture)),
                Csv(lead.BudgetMax?.ToString(CultureInfo.InvariantCulture)),
                Csv(lead.Owner?.Name), Csv(lead.Branch?.Name),
                Csv(lead.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
            }));
        }

        // The BOM is what makes Excel open this as UTF-8 rather than mangling
        // every name with a diacritic in it.
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();

        return File(bytes, "text/csv", $"leads-{DateTime.UtcNow:yyyy-MM-dd}.csv");
    }

    /* ---------------- mass transfer ---------------- */

    /// <summary>
    /// Moves ownership of many records at once.
    ///
    /// Preview first, by default. A transfer is not reversible in any useful
    /// sense — once four hundred leads have changed hands the previous owner is
    /// not recorded on them — so the screen shows what would move before it
    /// moves anything.
    /// </summary>
    [HttpPost("mass-transfer")]
    public async Task<ActionResult<MassTransferResultDto>> MassTransfer(
        MassTransferRequest input, CancellationToken ct)
    {
        var target = await db.Users.FirstOrDefaultAsync(u => u.Id == input.ToUserId && u.IsActive, ct);
        if (target is null) return BadRequest(new { message = "That account is not active." });

        if (input.FromUserId == input.ToUserId)
        {
            return BadRequest(new { message = "Those are the same person." });
        }

        var query = db.Leads.AsQueryable();

        query = input.FromUserId is int from
            ? query.Where(l => l.OwnerId == from)
            : query.Where(l => l.OwnerId == null);

        if (!string.IsNullOrWhiteSpace(input.Stage)) query = query.Where(l => l.Stage == input.Stage);
        if (!string.IsNullOrWhiteSpace(input.Source)) query = query.Where(l => l.Source == input.Source);
        if (!string.IsNullOrWhiteSpace(input.City)) query = query.Where(l => l.City == input.City);

        var matched = await query.CountAsync(ct);

        var sample = await query
            .OrderByDescending(l => l.CreatedAt)
            .Take(5)
            .Select(l => l.Name + (l.City == null ? "" : " · " + l.City))
            .ToListAsync(ct);

        if (input.Preview)
        {
            return Ok(new MassTransferResultDto(matched, 0, true, sample));
        }

        var moved = await query.ExecuteUpdateAsync(
            l => l.SetProperty(x => x.OwnerId, input.ToUserId)
                  .SetProperty(x => x.UpdatedAt, DateTime.UtcNow),
            ct);

        return Ok(new MassTransferResultDto(matched, moved, false, sample));
    }

    /* ---------------- helpers ---------------- */

    private static string? Text(Dictionary<string, string?> values, string key)
    {
        if (!values.TryGetValue(key, out var value)) return null;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static decimal? Money(Dictionary<string, string?> values, string key)
    {
        var raw = Text(values, key);
        if (raw is null) return null;

        // Spreadsheets hand back "₹ 45,00,000" as readily as "4500000".
        var cleaned = new string(raw.Where(c => char.IsDigit(c) || c == '.').ToArray());

        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// Reads an event date out of a spreadsheet cell.
    ///
    /// Day-first formats are tried before the invariant parse, because a sheet
    /// exported anywhere in India writes 04/11/2026 for the fourth of November
    /// and the invariant reading would file the wedding in April.
    /// </summary>
    private static DateTime? Date(Dictionary<string, string?> values, string key)
    {
        var raw = Text(values, key);
        if (raw is null) return null;

        string[] dayFirst =
        [
            "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy",
            "yyyy-MM-dd", "dd MMM yyyy", "d MMM yyyy", "dd MMMM yyyy", "d MMMM yyyy",
        ];

        if (DateTime.TryParseExact(
                raw, dayFirst, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var exact))
        {
            return DateTime.SpecifyKind(exact.Date, DateTimeKind.Utc);
        }

        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc)
            : null;
    }

    /// <summary>Reads a whole number, tolerating the commas a spreadsheet adds.</summary>
    private static int? Whole(Dictionary<string, string?> values, string key)
    {
        var raw = Text(values, key);
        if (raw is null) return null;

        var cleaned = new string(raw.Where(char.IsDigit).ToArray());

        return int.TryParse(cleaned, out var parsed) ? parsed : null;
    }

    /// <summary>Quotes a CSV field, doubling any quotes inside it.</summary>
    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;

        var needsQuotes = value.Contains(',') || value.Contains('"')
            || value.Contains('\n') || value.Contains('\r');

        return needsQuotes ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }

    /// <summary>The innermost message, which is the one that says what actually went wrong.</summary>
    private static string Root(Exception error)
    {
        var current = error;
        while (current.InnerException is not null) current = current.InnerException;

        return current.Message.Length > 200 ? current.Message[..200] : current.Message;
    }
}
