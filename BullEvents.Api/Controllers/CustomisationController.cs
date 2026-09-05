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

public record CustomFieldDto(
    int Id,
    string Object,
    string Key,
    string Label,
    string? HelpText,
    string Type,
    string[] Options,
    bool Required,
    bool ShowInList,
    int SortOrder,
    bool IsActive,
    /// <summary>How many records already carry a value here — what a delete would strand.</summary>
    int UsedBy);

public record SaveCustomFieldRequest(
    [Required] string Object,
    [Required, MinLength(2)] string Label,
    string? HelpText,
    [Required] string Type,
    string[]? Options,
    bool Required,
    bool ShowInList,
    int SortOrder,
    bool IsActive);

public record PickListValueDto(
    int Id, string List, string Value, string Label, int SortOrder, bool IsActive, bool IsSystem);

public record PickListDto(
    string Key, string Label, string Description, IReadOnlyList<PickListValueDto> Values);

public record SavePickListValueRequest(
    [Required, MinLength(1)] string Label,
    int SortOrder,
    bool IsActive);

public record CreatePickListValueRequest(
    [Required, MinLength(1)] string Label,
    int SortOrder);

public record BrandingDto(string CompanyName, string? BrandColor, string? LogoUrl);

public record SaveBrandingRequest(string? BrandColor, string? LogoUrl);

/// <summary>One thing a new workspace still has to do before it is usable.</summary>
public record SetupStepDto(
    string Key,
    string Title,
    string Description,
    bool Done,
    /// <summary>What has been done so far, for a step that is a count rather than a yes/no.</summary>
    int Progress,
    int Target,
    string? Href);

/* ------------------------------------------------------------------ *
 * Controller
 * ------------------------------------------------------------------ */

/// <summary>
/// The screens a company uses to make the CRM its own: the fields it added, the
/// vocabulary it uses, and what its customers see on a quotation.
///
/// Gated on administering users for the same reason the access console is —
/// these settings change what everybody in the company sees, which is an
/// administrator's decision rather than a preference.
/// </summary>
[ApiController]
[Route("api/admin/customisation")]
[Authorize]
[RequirePermission(SecuredObjects.User, ObjectAction.ModifyAll)]
public class CustomisationController(
    AppDbContext db,
    CustomFieldService customFields,
    EntitlementService entitlements) : ControllerBase
{
    /* ---------------- custom fields ---------------- */

    /// <summary>The objects a company may extend, so the client does not hard-code them.</summary>
    [HttpGet("fields/objects")]
    public ActionResult<IReadOnlyList<string>> ExtendableObjects() =>
        Ok(ExtendableObjectKeys);

    private static readonly string[] ExtendableObjectKeys =
        [SecuredObjects.Lead, SecuredObjects.Contact, SecuredObjects.Opportunity];

    [HttpGet("fields")]
    public async Task<ActionResult<IReadOnlyList<CustomFieldDto>>> Fields(
        [FromQuery] string? securedObject, CancellationToken ct)
    {
        var query = db.CustomFieldDefinitions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(securedObject))
        {
            query = query.Where(f => f.Object == securedObject);
        }

        var definitions = await query
            .OrderBy(f => f.Object).ThenBy(f => f.SortOrder).ThenBy(f => f.Id)
            .ToListAsync(ct);

        var result = new List<CustomFieldDto>(definitions.Count);
        foreach (var definition in definitions)
        {
            result.Add(ToDto(definition, await CountUsageAsync(definition, ct)));
        }

        return Ok(result);
    }

    [HttpPost("fields")]
    public async Task<ActionResult<CustomFieldDto>> CreateField(
        SaveCustomFieldRequest request, CancellationToken ct)
    {
        // Custom fields are a paid capability, so the plan is asked before the
        // row is written rather than the screen being hidden and the API left open.
        await entitlements.EnsureModuleAsync(Modules.Administration, ct);

        var plan = (await entitlements.ResolveAsync(ct: ct)).Plan;
        if (!plan.CustomFields)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = $"Custom fields are not part of the {plan.Name} plan.",
            });
        }

        if (!ExtendableObjectKeys.Contains(request.Object, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = $"'{request.Object}' cannot carry custom fields." });
        }

        if (!Enum.TryParse<CustomFieldType>(request.Type, true, out var type))
        {
            return BadRequest(new { message = $"'{request.Type}' is not a field type." });
        }

        if (type == CustomFieldType.Select && (request.Options is null || request.Options.Length == 0))
        {
            return BadRequest(new { message = "A choice field needs at least one option." });
        }

        var key = await UniqueKeyAsync(request.Object, request.Label, ct);

        var definition = new CustomFieldDefinition
        {
            CompanyId = User.GetCompanyId(),
            Object = ExtendableObjectKeys.First(o =>
                string.Equals(o, request.Object, StringComparison.OrdinalIgnoreCase)),
            Key = key,
            Label = request.Label.Trim(),
            HelpText = Blank(request.HelpText),
            Type = type,
            OptionsCsv = request.Options is { Length: > 0 } ? string.Join(',', request.Options) : null,
            Required = request.Required,
            ShowInList = request.ShowInList,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
        };

        db.CustomFieldDefinitions.Add(definition);
        await db.SaveChangesAsync(ct);

        return Ok(ToDto(definition, 0));
    }

    /// <summary>
    /// Changes a field's presentation. The key and the object are not editable:
    /// records already carry values under that key, and moving it would orphan
    /// every one of them.
    /// </summary>
    [HttpPut("fields/{id:int}")]
    public async Task<ActionResult<CustomFieldDto>> UpdateField(
        int id, SaveCustomFieldRequest request, CancellationToken ct)
    {
        var definition = await db.CustomFieldDefinitions.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (definition is null) return NotFound(new { message = "That field no longer exists." });

        if (!Enum.TryParse<CustomFieldType>(request.Type, true, out var type))
        {
            return BadRequest(new { message = $"'{request.Type}' is not a field type." });
        }

        if (type != definition.Type && await CountUsageAsync(definition, ct) > 0)
        {
            return BadRequest(new
            {
                message = "This field already holds values, so its type cannot change. "
                        + "Retire it and add a new one instead.",
            });
        }

        definition.Label = request.Label.Trim();
        definition.HelpText = Blank(request.HelpText);
        definition.Type = type;
        definition.OptionsCsv = request.Options is { Length: > 0 }
            ? string.Join(',', request.Options)
            : null;
        definition.Required = request.Required;
        definition.ShowInList = request.ShowInList;
        definition.SortOrder = request.SortOrder;
        definition.IsActive = request.IsActive;
        definition.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(ToDto(definition, await CountUsageAsync(definition, ct)));
    }

    /// <summary>
    /// Removes a field that nothing has used. One that holds values is retired
    /// instead, because deleting the definition would leave data in the records
    /// that nothing can read or clear.
    /// </summary>
    [HttpDelete("fields/{id:int}")]
    public async Task<IActionResult> DeleteField(int id, CancellationToken ct)
    {
        var definition = await db.CustomFieldDefinitions.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (definition is null) return NotFound(new { message = "That field no longer exists." });

        var used = await CountUsageAsync(definition, ct);

        if (used > 0)
        {
            return Conflict(new
            {
                message = $"{used:N0} records carry a value in this field. "
                        + "Retire it instead — the values stay, and the field stops appearing.",
            });
        }

        db.CustomFieldDefinitions.Remove(definition);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /* ---------------- pick lists ---------------- */

    [HttpGet("pick-lists")]
    public async Task<ActionResult<IReadOnlyList<PickListDto>>> AllPickLists(CancellationToken ct)
    {
        var values = await db.PickListValues
            .OrderBy(v => v.SortOrder).ThenBy(v => v.Id)
            .ToListAsync(ct);

        return Ok(PickLists.All
            .Select(list => new PickListDto(
                list,
                PickLists.Label(list),
                PickLists.Describe(list),
                values
                    .Where(v => string.Equals(v.List, list, StringComparison.OrdinalIgnoreCase))
                    .Select(ToDto)
                    .ToList()))
            .ToList());
    }

    [HttpPost("pick-lists/{list}")]
    public async Task<ActionResult<PickListValueDto>> AddValue(
        string list, CreatePickListValueRequest request, CancellationToken ct)
    {
        if (!PickLists.Exists(list)) return NotFound(new { message = "No such list." });

        var label = request.Label.Trim();

        // The stored value is the label at the moment of creation and never
        // changes again. Renaming later moves the label only, so the records
        // already carrying it keep resolving.
        if (await db.PickListValues.AnyAsync(
                v => v.List == list && v.Value == label, ct))
        {
            return Conflict(new { message = $"'{label}' is already in this list." });
        }

        var value = new PickListValue
        {
            CompanyId = User.GetCompanyId(),
            List = PickLists.All.First(l => string.Equals(l, list, StringComparison.OrdinalIgnoreCase)),
            Value = label,
            Label = label,
            SortOrder = request.SortOrder,
        };

        db.PickListValues.Add(value);
        await db.SaveChangesAsync(ct);

        return Ok(ToDto(value));
    }

    [HttpPut("pick-lists/values/{id:int}")]
    public async Task<ActionResult<PickListValueDto>> UpdateValue(
        int id, SavePickListValueRequest request, CancellationToken ct)
    {
        var value = await db.PickListValues.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (value is null) return NotFound(new { message = "That entry no longer exists." });

        value.Label = request.Label.Trim();
        value.SortOrder = request.SortOrder;
        value.IsActive = request.IsActive;

        await db.SaveChangesAsync(ct);

        return Ok(ToDto(value));
    }

    [HttpDelete("pick-lists/values/{id:int}")]
    public async Task<IActionResult> DeleteValue(int id, CancellationToken ct)
    {
        var value = await db.PickListValues.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (value is null) return NotFound(new { message = "That entry no longer exists." });

        if (value.IsSystem)
        {
            return Conflict(new
            {
                message = "This entry ships with the product and is referenced by the "
                        + "seeded data. Rename it, or switch it off.",
            });
        }

        db.PickListValues.Remove(value);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /* ---------------- branding ---------------- */

    [HttpGet("branding")]
    public async Task<ActionResult<BrandingDto>> Branding(CancellationToken ct)
    {
        var company = await db.Companies.FirstAsync(c => c.Id == User.GetCompanyId(), ct);
        return Ok(new BrandingDto(company.Name, company.BrandColor, company.LogoUrl));
    }

    /// <summary>
    /// Sets what a customer sees on a quotation and the public quote page — the
    /// only two places the CRM shows itself to somebody outside the company.
    /// </summary>
    [HttpPut("branding")]
    public async Task<ActionResult<BrandingDto>> SaveBranding(
        SaveBrandingRequest request, CancellationToken ct)
    {
        var company = await db.Companies.FirstAsync(c => c.Id == User.GetCompanyId(), ct);

        var color = Blank(request.BrandColor);

        // Validated because it is interpolated into a stylesheet and a PDF: an
        // arbitrary string there is a small injection and a large support ticket.
        if (color is not null && !IsHexColor(color))
        {
            return BadRequest(new { message = "The brand colour must be a hex value such as #1F4FD8." });
        }

        var logo = Blank(request.LogoUrl);

        if (logo is not null
            && !(Uri.TryCreate(logo, UriKind.Absolute, out var uri)
                 && uri.Scheme is "http" or "https"))
        {
            return BadRequest(new { message = "The logo must be a full http or https URL." });
        }

        company.BrandColor = color;
        company.LogoUrl = logo;

        await db.SaveChangesAsync(ct);

        return Ok(new BrandingDto(company.Name, company.BrandColor, company.LogoUrl));
    }

    /* ---------------- setup checklist ---------------- */

    /// <summary>
    /// What a new workspace still has to do, counted from the data rather than
    /// from a stored "wizard completed" flag.
    ///
    /// Reading the real state means the list is honest after somebody deletes
    /// their only branch, and a company that arrived through an import starts
    /// with most of it already ticked.
    /// </summary>
    [HttpGet("setup")]
    public async Task<ActionResult<IReadOnlyList<SetupStepDto>>> Setup(CancellationToken ct)
    {
        var companyId = User.GetCompanyId();

        var branches = await db.Branches.CountAsync(ct);
        var users = await db.Users.CountAsync(u => u.IsActive, ct);
        var managed = await db.Users.CountAsync(u => u.ManagerId != null, ct);
        var projects = await db.Projects.CountAsync(ct);
        var leads = await db.Leads.CountAsync(ct);
        var lists = await db.PickListValues.CountAsync(v => !v.IsSystem, ct);

        var company = await db.Companies.FirstAsync(c => c.Id == companyId, ct);

        return Ok(new List<SetupStepDto>
        {
            new("branches", "Add your offices",
                "Leads, users and inventory are all scoped to a branch.",
                branches > 0, branches, 1, "/dashboard/branches"),

            new("users", "Invite your team",
                "Everyone who will work the pipeline needs a seat.",
                users > 1, users, 2, "/dashboard/users"),

            new("hierarchy", "Set the reporting lines",
                "A team-scoped role sees exactly the people beneath it here.",
                managed > 0, managed, 1, "/dashboard/users/teams"),

            new("inventory", "Load what you are selling",
                "Projects, towers and units — the stock quotations are built from.",
                projects > 0, projects, 1, "/dashboard/sales/inventory"),

            new("vocabulary", "Match the lists to how you talk",
                "Lead sources and loss reasons ship with defaults. Rename them to yours.",
                lists > 0, lists, 1, "/dashboard/companies/customisation"),

            new("branding", "Put your name on the quotation",
                "The logo and colour customers see on a quote and its public link.",
                company.LogoUrl is not null || company.BrandColor is not null,
                company.LogoUrl is not null || company.BrandColor is not null ? 1 : 0, 1,
                "/dashboard/companies/customisation"),

            new("leads", "Bring in your leads",
                "Import the pipeline you already have, or start capturing new enquiries.",
                leads > 0, leads, 1, "/dashboard/leads"),
        });
    }

    /* ---------------- helpers ---------------- */

    private static CustomFieldDto ToDto(CustomFieldDefinition f, int usedBy) => new(
        f.Id, f.Object, f.Key, f.Label, f.HelpText, f.Type.ToString(), f.Options,
        f.Required, f.ShowInList, f.SortOrder, f.IsActive, usedBy);

    private static PickListValueDto ToDto(PickListValue v) =>
        new(v.Id, v.List, v.Value, v.Label, v.SortOrder, v.IsActive, v.IsSystem);

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsHexColor(string value) =>
        value.Length is 4 or 7
        && value[0] == '#'
        && value[1..].All(Uri.IsHexDigit);

    private async Task<string> UniqueKeyAsync(string securedObject, string label, CancellationToken ct)
    {
        var baseKey = CustomFieldService.KeyFor(label);
        var key = baseKey;

        for (var suffix = 2; suffix < 100; suffix++)
        {
            var taken = await db.CustomFieldDefinitions
                .AnyAsync(f => f.Object == securedObject && f.Key == key, ct);

            if (!taken) return key;
            key = $"{baseKey}-{suffix}";
        }

        return $"{baseKey}-{Guid.NewGuid():N}"[..48];
    }

    /// <summary>
    /// How many records already carry a value under this key.
    ///
    /// Asked before a type change or a delete, because both are safe on an empty
    /// field and destructive on a used one — and the difference is not something
    /// an administrator can see from the screen.
    /// </summary>
    private async Task<int> CountUsageAsync(CustomFieldDefinition definition, CancellationToken ct)
    {
        // JsonExists compiles to the jsonb `?` operator — a key lookup Postgres
        // can answer from a GIN index. The obvious alternative, a LIKE for the
        // quoted key, is both a full scan and illegal: these columns inherit the
        // non-deterministic collation the rest of the text carries, and Postgres
        // refuses LIKE against one.
        var key = definition.Key;

        return definition.Object switch
        {
            SecuredObjects.Lead => await db.Leads
                .CountAsync(l => EF.Functions.JsonExists(l.CustomFields!, key), ct),

            SecuredObjects.Contact => await db.Contacts
                .CountAsync(c => EF.Functions.JsonExists(c.CustomFields!, key), ct),

            SecuredObjects.Opportunity => await db.Opportunities
                .CountAsync(o => EF.Functions.JsonExists(o.CustomFields!, key), ct),

            _ => 0,
        };
    }
}
