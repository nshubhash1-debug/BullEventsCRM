using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using BullEvents.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

public record ChoiceDto(string Value, string Label);

public record FormFieldDto(
    string Key,
    string Label,
    string? HelpText,
    string Type,
    string[] Options,
    bool Required,
    bool ShowInList,
    int SortOrder);

/// <summary>
/// Everything the client needs to draw this company's forms: the vocabulary it
/// uses, the fields it added, and what its quotations look like.
///
/// Separate from the customisation controller, and readable by anybody signed
/// in, because these are not settings — they are the shape of the screens. A
/// sales executive filling in a lead needs the source list; gating it behind
/// "may administer users" would leave them with an empty dropdown.
/// </summary>
[ApiController]
[Route("api/workspace")]
[Authorize]
public class WorkspaceConfigController(AppDbContext db, CustomFieldService customFields)
    : ControllerBase
{
    /// <summary>
    /// One list, ready to render. Retired values are left out — they exist so
    /// old records still read correctly, not so new ones can pick them.
    /// </summary>
    [HttpGet("pick-lists/{list}")]
    public async Task<ActionResult<IReadOnlyList<ChoiceDto>>> PickList(
        string list, CancellationToken ct)
    {
        if (!PickLists.Exists(list)) return NotFound(new { message = "No such list." });

        var values = await db.PickListValues
            .Where(v => v.List == list && v.IsActive)
            .OrderBy(v => v.SortOrder).ThenBy(v => v.Id)
            .Select(v => new ChoiceDto(v.Value, v.Label))
            .ToListAsync(ct);

        return Ok(values);
    }

    /// <summary>Every list at once — one request for a form that needs several.</summary>
    [HttpGet("pick-lists")]
    public async Task<ActionResult<IReadOnlyDictionary<string, IReadOnlyList<ChoiceDto>>>> AllPickLists(
        CancellationToken ct)
    {
        var values = await db.PickListValues
            .Where(v => v.IsActive)
            .OrderBy(v => v.SortOrder).ThenBy(v => v.Id)
            .ToListAsync(ct);

        var grouped = PickLists.All.ToDictionary(
            list => list,
            list => (IReadOnlyList<ChoiceDto>)values
                .Where(v => string.Equals(v.List, list, StringComparison.OrdinalIgnoreCase))
                .Select(v => new ChoiceDto(v.Value, v.Label))
                .ToList(),
            StringComparer.OrdinalIgnoreCase);

        return Ok(grouped);
    }

    /// <summary>The fields this company added to an object, in display order.</summary>
    [HttpGet("fields/{securedObject}")]
    public async Task<ActionResult<IReadOnlyList<FormFieldDto>>> Fields(
        string securedObject, CancellationToken ct)
    {
        if (!SecuredObjects.All.Contains(securedObject, StringComparer.OrdinalIgnoreCase))
        {
            return NotFound(new { message = "No such object." });
        }

        var definitions = await customFields.DefinitionsAsync(securedObject, ct: ct);

        return Ok(definitions
            .Select(f => new FormFieldDto(
                f.Key, f.Label, f.HelpText, f.Type.ToString(), f.Options,
                f.Required, f.ShowInList, f.SortOrder))
            .ToList());
    }

    /// <summary>What this workspace looks like on a quotation.</summary>
    [HttpGet("branding")]
    public async Task<ActionResult<BrandingDto>> Branding(CancellationToken ct)
    {
        var company = await db.Companies.FirstAsync(c => c.Id == User.GetCompanyId(), ct);
        return Ok(new BrandingDto(company.Name, company.BrandColor, company.LogoUrl));
    }
}
