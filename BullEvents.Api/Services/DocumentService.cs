using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>
/// Produces the letters this business sends, and keeps the copies.
///
/// The rendered body is stored rather than re-rendered on demand. A demand
/// letter is a commercial document: when the template is edited next month, the
/// copy the customer is holding must still be the copy this system can show.
/// Every dispute in this business turns on exactly that — "you never told me"
/// is answered by producing the letter as it went out, not as it would render
/// today.
/// </summary>
public class DocumentService(AppDbContext db, DocumentComposer composer, TenantContext tenant)
{
    /// <summary>
    /// Renders and stores one document.
    ///
    /// The template is chosen explicitly or, failing that, the default for its
    /// kind. A kind with no default is an error rather than a silent skip: a
    /// bulk demand run that quietly produced nothing is worse than one that
    /// stops and says why.
    /// </summary>
    public async Task<GeneratedDocument> GenerateAsync(
        string kind,
        DocumentComposer.Context context,
        int? templateId = null,
        CancellationToken ct = default)
    {
        var template = templateId is int id
            ? await db.DocumentTemplates.FirstOrDefaultAsync(t => t.Id == id && t.IsActive, ct)
            : await db.DocumentTemplates
                .Where(t => t.Kind == kind && t.IsActive)
                .OrderByDescending(t => t.IsDefault)
                .FirstOrDefaultAsync(ct);

        if (template is null)
        {
            throw ApiException.BadRequest(
                $"There is no active template for '{kind}'. Add one under Document templates.");
        }

        if (TemplateKinds.NeedDemand.Contains(template.Kind) && context.DemandId is null)
        {
            throw ApiException.BadRequest($"A {template.Name.ToLowerInvariant()} needs a demand.");
        }

        if (TemplateKinds.NeedReceipt.Contains(template.Kind) && context.ReceiptId is null)
        {
            throw ApiException.BadRequest($"A {template.Name.ToLowerInvariant()} needs a receipt.");
        }

        var number = await NextNumberAsync(template.Kind, ct);

        // The number is resolved before the body renders, because the letterhead
        // prints it — a document that refers to itself has to know its own
        // reference first.
        var (body, _) = await composer.RenderAsync(
            template.Body.Replace("{{document.number}}", number), context, ct);

        var (subject, _) = await composer.RenderAsync(
            (template.Subject ?? template.Name).Replace("{{document.number}}", number), context, ct);

        var document = new GeneratedDocument
        {
            CompanyId = tenant.CompanyId,
            BookingId = context.BookingId,
            DemandId = context.DemandId,
            ReceiptId = context.ReceiptId,
            DocumentTemplateId = template.Id,
            Kind = template.Kind,
            Number = number,
            Title = template.Name,
            Subject = subject,
            Body = body,
            Status = GeneratedStatuses.Draft,
            GeneratedAt = DateTime.UtcNow,
        };

        db.GeneratedDocuments.Add(document);
        await db.SaveChangesAsync(ct);

        return document;
    }

    /// <summary>
    /// Produces one letter for every open demand matching a filter.
    ///
    /// This is what makes the feature worth having. Raising demands for a whole
    /// slab is one click; writing three hundred letters afterwards, one at a
    /// time, is what stops anybody doing it.
    /// </summary>
    public async Task<(int Produced, int Skipped, List<string> Notes)> BulkAsync(
        string kind,
        int[] demandIds,
        int? templateId = null,
        CancellationToken ct = default)
    {
        var demands = await db.Demands
            .Where(d => demandIds.Contains(d.Id) && d.Status != DemandStatuses.Cancelled)
            .Select(d => new { d.Id, d.BookingId, d.DemandNumber })
            .ToListAsync(ct);

        var produced = 0;
        var skipped = 0;
        var notes = new List<string>();

        foreach (var demand in demands)
        {
            try
            {
                await GenerateAsync(
                    kind,
                    new DocumentComposer.Context(demand.BookingId, demand.Id),
                    templateId,
                    ct);

                produced++;
            }
            catch (Exception error)
            {
                // One booking with missing data must not stop the run. The note
                // names which one, so it can be fixed and re-run for that alone.
                skipped++;
                if (notes.Count < 20) notes.Add($"{demand.DemandNumber}: {error.Message}");
            }
        }

        return (produced, skipped, notes);
    }

    /// <summary>Marks a document issued — the point it becomes the record copy.</summary>
    public async Task<GeneratedDocument> IssueAsync(int documentId, CancellationToken ct = default)
    {
        var document = await db.GeneratedDocuments.FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw ApiException.NotFound("Document");

        if (document.Status == GeneratedStatuses.Cancelled)
        {
            throw ApiException.BadRequest("That document was cancelled.");
        }

        document.Status = GeneratedStatuses.Issued;
        document.IssuedAt ??= DateTime.UtcNow;
        document.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return document;
    }

    /// <summary>
    /// Records that a document went out.
    ///
    /// Marks rather than sends: no mail provider is connected, and a method that
    /// claimed to have emailed something would be a lie told by the audit trail.
    /// The channel and address are stored so that when a provider is wired in,
    /// the history already has the shape it needs.
    /// </summary>
    public async Task<GeneratedDocument> MarkSentAsync(
        int documentId, string via, string? to, CancellationToken ct = default)
    {
        var document = await db.GeneratedDocuments.FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw ApiException.NotFound("Document");

        document.Status = GeneratedStatuses.Sent;
        document.IssuedAt ??= DateTime.UtcNow;
        document.SentAt = DateTime.UtcNow;
        document.SentVia = via;
        document.SentTo = to;
        document.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return document;
    }

    /// <summary>
    /// The next reference for a kind — BRG/DEM/2026/0007.
    ///
    /// Collated to "C" before the prefix comparison. The text columns carry a
    /// case-insensitive, non-deterministic collation and Postgres refuses
    /// pattern matching against one; this is the fourth place in this codebase
    /// that has had to say so.
    /// </summary>
    private async Task<string> NextNumberAsync(string kind, CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var stem = $"{Abbreviation(kind)}/{year}/";

        var last = await db.GeneratedDocuments
            .IgnoreQueryFilters()
            .Where(d => d.CompanyId == tenant.CompanyId
                && EF.Functions.Collate(d.Number, "C").StartsWith(stem))
            .OrderByDescending(d => d.Number)
            .Select(d => d.Number)
            .FirstOrDefaultAsync(ct);

        var next = 1;

        if (last is not null && int.TryParse(last[stem.Length..], out var parsed))
        {
            next = parsed + 1;
        }

        return $"{stem}{next:D4}";
    }

    private static string Abbreviation(string kind) => kind switch
    {
        TemplateKinds.DemandLetter => "DEM",
        TemplateKinds.ReminderLetter => "REM",
        TemplateKinds.AllotmentLetter => "ALT",
        TemplateKinds.WelcomeLetter => "WEL",
        TemplateKinds.PaymentReceipt => "RCT",
        TemplateKinds.StatementOfAccount => "SOA",
        TemplateKinds.PossessionOffer => "POF",
        TemplateKinds.PossessionLetter => "POS",
        TemplateKinds.NocForLoan => "NOC",
        TemplateKinds.CancellationLetter => "CAN",
        TemplateKinds.TransferLetter => "TRF",
        TemplateKinds.BrokerageInvoice => "BRK",
        _ => "DOC",
    };
}
