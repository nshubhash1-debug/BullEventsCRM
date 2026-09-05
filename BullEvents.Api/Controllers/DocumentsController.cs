using System.ComponentModel.DataAnnotations;
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

public record TemplateDto(
    int Id,
    string Kind,
    string Name,
    string? Description,
    string? Subject,
    string Body,
    bool IsDefault,
    bool IsSystem,
    bool IsActive,
    /// <summary>How many letters this template has produced.</summary>
    int UsedCount);

public record SaveTemplateRequest(
    [Required] string Kind,
    [Required] string Name,
    string? Description,
    string? Subject,
    [Required] string Body,
    bool IsDefault,
    bool IsActive);

public record MergeFieldDto(string Token, string Label, string Group, string Example);

public record TemplateReferenceDto(
    string[] Kinds,
    IReadOnlyList<MergeFieldDto> MergeFields);

public record PreviewRequest(
    [Required] string Body,
    string? Subject,
    int BookingId,
    int? DemandId,
    int? ReceiptId);

public record PreviewDto(string Subject, string Body, IReadOnlyList<string> Unresolved);

public record GeneratedDocumentDto(
    int Id,
    int BookingId,
    int? DemandId,
    string Kind,
    string Number,
    string Title,
    string? Subject,
    string Status,
    DateTime GeneratedAt,
    DateTime? IssuedAt,
    DateTime? SentAt,
    string? SentTo,
    string? SentVia);

public record GenerateRequest(
    [Required] string Kind,
    int? TemplateId,
    int? DemandId,
    int? ReceiptId);

public record BulkGenerateRequest(
    [Required] string Kind,
    int? TemplateId,
    [Required, MinLength(1)] int[] DemandIds);

public record BulkGenerateResultDto(int Produced, int Skipped, IReadOnlyList<string> Notes);

public record MarkSentRequest([Required] string Via, string? To);

/* ------------------------------------------------------------------ *
 * Controller
 * ------------------------------------------------------------------ */

/// <summary>
/// The letters this business sends, and the templates they come from.
///
/// Templates are edited by the people who own the wording — a demand letter's
/// clauses are a commercial and legal decision that changes without a
/// deployment, and a developer should never be the bottleneck on "add the GST
/// number to the footer".
/// </summary>
[ApiController]
[Route("api/post-sales/documents")]
[Authorize]
[SecuredBy(SecuredObjects.Booking)]
[RequireModule(Modules.PostSales)]
public class DocumentsController(
    AppDbContext db,
    DocumentService documents,
    DocumentComposer composer) : CrmControllerBase(db)
{
    /* ---------------- reference ---------------- */

    /// <summary>The kinds a template can be, and every token it may contain.</summary>
    [HttpGet("reference")]
    public ActionResult<TemplateReferenceDto> Reference()
        => Ok(new TemplateReferenceDto(
            TemplateKinds.All,
            MergeFields.All
                .Select(f => new MergeFieldDto(f.Token, f.Label, f.Group, f.Example))
                .ToList()));

    /* ---------------- templates ---------------- */

    [HttpGet("templates")]
    public async Task<ActionResult<IReadOnlyList<TemplateDto>>> Templates(CancellationToken ct)
        => Ok(await TemplatesAsync(ct));

    private async Task<List<TemplateDto>> TemplatesAsync(CancellationToken ct)
    {
        var templates = await Db.DocumentTemplates
            .OrderBy(t => t.Kind).ThenBy(t => t.Name)
            .ToListAsync(ct);

        var used = await Db.GeneratedDocuments
            .Where(d => d.DocumentTemplateId != null)
            .GroupBy(d => d.DocumentTemplateId!.Value)
            .Select(g => new { TemplateId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TemplateId, x => x.Count, ct);

        return templates.Select(t => new TemplateDto(
            t.Id, t.Kind, t.Name, t.Description, t.Subject, t.Body,
            t.IsDefault, t.IsSystem, t.IsActive,
            used.GetValueOrDefault(t.Id))).ToList();
    }

    [HttpPost("templates")]
    public async Task<ActionResult<TemplateDto>> CreateTemplate(
        SaveTemplateRequest input, CancellationToken ct)
    {
        if (!TemplateKinds.All.Contains(input.Kind))
        {
            return BadRequest(new { message = $"'{input.Kind}' is not a document this system writes." });
        }

        var template = new DocumentTemplate { CompanyId = User.GetCompanyId() };

        await ApplyAsync(template, input, ct);

        Db.DocumentTemplates.Add(template);
        await Db.SaveChangesAsync(ct);

        return Ok((await TemplatesAsync(ct)).First(t => t.Id == template.Id));
    }

    [HttpPut("templates/{id:int}")]
    public async Task<ActionResult<TemplateDto>> UpdateTemplate(
        int id, SaveTemplateRequest input, CancellationToken ct)
    {
        var template = await Db.DocumentTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return NotFound(new { message = "That template no longer exists." });

        await ApplyAsync(template, input, ct);
        await Db.SaveChangesAsync(ct);

        return Ok((await TemplatesAsync(ct)).First(t => t.Id == id));
    }

    [HttpDelete("templates/{id:int}")]
    public async Task<IActionResult> DeleteTemplate(int id, CancellationToken ct)
    {
        var template = await Db.DocumentTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return NoContent();

        if (template.IsSystem)
        {
            return BadRequest(new
            {
                message = "A template that ships with the product can be edited or switched off, not deleted.",
            });
        }

        Db.DocumentTemplates.Remove(template);
        await Db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Renders a draft body against a real booking without storing anything.
    ///
    /// This is what makes the editor usable: an author needs to see the letter
    /// with real numbers in it, and the list of tokens that did not resolve, before
    /// three hundred copies go out with a blank where the amount should be.
    /// </summary>
    [HttpPost("preview")]
    public async Task<ActionResult<PreviewDto>> Preview(PreviewRequest input, CancellationToken ct)
    {
        var context = new DocumentComposer.Context(input.BookingId, input.DemandId, input.ReceiptId);

        var (body, unresolved) = await composer.RenderAsync(
            input.Body.Replace("{{document.number}}", "PREVIEW"), context, ct);

        var (subject, _) = await composer.RenderAsync(
            (input.Subject ?? string.Empty).Replace("{{document.number}}", "PREVIEW"), context, ct);

        return Ok(new PreviewDto(subject, body, unresolved));
    }

    /* ---------------- generated documents ---------------- */

    [HttpGet("/api/bookings/{bookingId:int}/letters")]
    public async Task<ActionResult<IReadOnlyList<GeneratedDocumentDto>>> ForBooking(
        int bookingId, CancellationToken ct)
        => Ok(await Db.GeneratedDocuments
            .Where(d => d.BookingId == bookingId)
            .OrderByDescending(d => d.GeneratedAt)
            .Select(d => new GeneratedDocumentDto(
                d.Id, d.BookingId, d.DemandId, d.Kind, d.Number, d.Title, d.Subject,
                d.Status, d.GeneratedAt, d.IssuedAt, d.SentAt, d.SentTo, d.SentVia))
            .ToListAsync(ct));

    /// <summary>The stored body. Rendered as it went out, not as it would render today.</summary>
    [HttpGet("{id:int}/body")]
    public async Task<ActionResult<object>> Body(int id, CancellationToken ct)
    {
        var document = await Db.GeneratedDocuments.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (document is null) return NotFound(new { message = "That document no longer exists." });

        return Ok(new { document.Number, document.Title, document.Subject, document.Body });
    }

    [HttpPost("/api/bookings/{bookingId:int}/letters")]
    public async Task<ActionResult<GeneratedDocumentDto>> Generate(
        int bookingId, GenerateRequest input, CancellationToken ct)
    {
        var document = await documents.GenerateAsync(
            input.Kind,
            new DocumentComposer.Context(bookingId, input.DemandId, input.ReceiptId),
            input.TemplateId,
            ct);

        return Ok(ToDto(document));
    }

    /// <summary>
    /// One letter per demand, in a single pass.
    ///
    /// Raising demands for a whole slab is already one click. Writing the three
    /// hundred letters afterwards, one at a time, is what stops anybody doing it.
    /// </summary>
    [HttpPost("bulk")]
    public async Task<ActionResult<BulkGenerateResultDto>> Bulk(
        BulkGenerateRequest input, CancellationToken ct)
    {
        var (produced, skipped, notes) = await documents.BulkAsync(
            input.Kind, input.DemandIds, input.TemplateId, ct);

        return Ok(new BulkGenerateResultDto(produced, skipped, notes));
    }

    [HttpPost("{id:int}/issue")]
    public async Task<ActionResult<GeneratedDocumentDto>> Issue(int id, CancellationToken ct)
        => Ok(ToDto(await documents.IssueAsync(id, ct)));

    [HttpPost("{id:int}/sent")]
    public async Task<ActionResult<GeneratedDocumentDto>> MarkSent(
        int id, MarkSentRequest input, CancellationToken ct)
        => Ok(ToDto(await documents.MarkSentAsync(id, input.Via, input.To, ct)));

    /* ---------------- helpers ---------------- */

    private async Task ApplyAsync(
        DocumentTemplate template, SaveTemplateRequest input, CancellationToken ct)
    {
        template.Kind = input.Kind;
        template.Name = input.Name.Trim();
        template.Description = input.Description?.Trim();
        template.Subject = input.Subject?.Trim();
        template.Body = input.Body;
        template.IsActive = input.IsActive;
        template.UpdatedAt = DateTime.UtcNow;

        // Exactly one default per kind. Clearing the others here rather than
        // trusting the caller means a bulk run can never find two.
        if (input.IsDefault)
        {
            await Db.DocumentTemplates
                .Where(t => t.Id != template.Id && t.Kind == input.Kind && t.IsDefault)
                .ExecuteUpdateAsync(t => t.SetProperty(x => x.IsDefault, false), ct);
        }

        template.IsDefault = input.IsDefault;
    }

    private static GeneratedDocumentDto ToDto(GeneratedDocument d) => new(
        d.Id, d.BookingId, d.DemandId, d.Kind, d.Number, d.Title, d.Subject,
        d.Status, d.GeneratedAt, d.IssuedAt, d.SentAt, d.SentTo, d.SentVia);
}
