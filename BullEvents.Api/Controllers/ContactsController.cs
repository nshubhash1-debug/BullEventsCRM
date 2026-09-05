using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// The customer database. Backs both the "Contacts" and "Customer Database"
/// views — same table, different default filter, which is why they share an
/// endpoint rather than duplicating one.
/// </summary>
[ApiController]
[Route("api/contacts")]
[Authorize]
[SecuredBy(SecuredObjects.Contact)]
public class ContactsController(AppDbContext db) : CrmControllerBase(db)
{
    /* ------------------------------------------------------------------ *
     * Queryable surface
     * ------------------------------------------------------------------ */

    internal static readonly FieldMap<Contact> Fields = new FieldMap<Contact>()
        .Text("fullName", "Full name", searchable: true)
        .Text("firstName", "First name")
        .Text("lastName", "Last name")
        .Text("accountName", "Account", searchable: true)
        .Text("designation", "Designation")
        .Text("phone", "Mobile", searchable: true)
        .Text("phone2", "Alternate mobile")
        .Text("email", "Email", searchable: true)
        .Text("whatsAppNumber", "WhatsApp")
        .Select("type", "Type")
        .Select("lifecycleStage", "Lifecycle stage")
        .Select("source", "Source")
        .Select("segment", "AI segment")
        .Text("tags", "Tags", searchable: true)
        .Select("city", "City")
        .Select("state", "State")
        .Text("pincode", "Pincode")
        .Number("lifetimeValue", "Lifetime value")
        .Number("dealCount", "Deals")
        .Number("budgetMin", "Budget from")
        .Number("budgetMax", "Budget to")
        .Select("preferredConfiguration", "Preferred configuration")
        .Text("preferredLocality", "Preferred locality")
        .Bool("doNotCall", "Do not call")
        .Bool("doNotEmail", "Do not email")
        .Bool("whatsAppOptIn", "WhatsApp opt-in")
        .Select("ownerName", "Owner", "Owner.Name")
        .Select("branchName", "Branch", "Branch.Name")
        .Number("ownerId", "Owner ID")
        .Number("branchId", "Branch ID")
        .Date("lastActivityAt", "Last activity")
        .Date("dateOfBirth", "Date of birth")
        .Date("createdAt", "Created")
        .Date("updatedAt", "Last modified");

    private IQueryable<Contact> Base() => Db.Contacts
        .Include(c => c.Branch)
        .Include(c => c.Owner)
        .AsNoTracking();

    private static ContactDto ToDto(Contact c, int openOpportunities = 0) => new(
        c.Id, c.Salutation, c.FirstName, c.LastName, c.FullName, c.Designation, c.AccountName,
        c.Phone, c.Phone2, c.Email, c.WhatsAppNumber,
        c.Address, c.City, c.State, c.Country, c.Pincode,
        c.Type, c.LifecycleStage, c.Source, c.Tags, c.Segment,
        c.LifetimeValue, c.DealCount, c.BudgetMin, c.BudgetMax,
        c.PreferredConfiguration, c.PreferredLocality,
        c.DoNotCall, c.DoNotEmail, c.WhatsAppOptIn, c.PanNumber, c.Gstin, c.DateOfBirth,
        c.BranchId, c.Branch?.Name ?? "—", c.OwnerId, c.Owner?.Name,
        c.ConvertedFromLeadId, openOpportunities, c.LastActivityAt, c.CreatedAt, c.UpdatedAt);

    /* ------------------------------------------------------------------ *
     * Reads
     * ------------------------------------------------------------------ */

    [HttpGet("fields")]
    public async Task<ActionResult<IReadOnlyList<FilterFieldDto>>> GetFields(CancellationToken ct)
    {
        var cities = await Db.Contacts
            .Where(c => c.City != null && c.City != "")
            .Select(c => c.City!)
            .Distinct().OrderBy(c => c).Take(200).ToListAsync(ct);

        var owners = await Db.Users
            .Where(u => u.CompanyId == Db.Tenant.CompanyId && u.IsActive)
            .Select(u => u.Name).OrderBy(n => n).ToListAsync(ct);

        var branches = await Db.Branches
            .Where(b => b.CompanyId == Db.Tenant.CompanyId)
            .Select(b => b.Name).OrderBy(n => n).ToListAsync(ct);

        var segments = await Db.Contacts
            .Where(c => c.Segment != null)
            .Select(c => c.Segment!)
            .Distinct().ToListAsync(ct);

        return Ok(DescribeFields(Fields, new Dictionary<string, IReadOnlyList<FilterOptionDto>>
        {
            ["type"] = Options(ContactTypes.All),
            ["lifecycleStage"] = Options(LifecycleStages.All),
            ["source"] = Options(LeadSources.All),
            ["preferredConfiguration"] = Options(SpaceTypes.All),
            ["segment"] = Options([.. segments]),
            ["city"] = Options([.. cities]),
            ["ownerName"] = Options([.. owners]),
            ["branchName"] = Options([.. branches]),
        }, new Dictionary<string, string>
        {
            ["fullName"] = "Identity",
            ["firstName"] = "Identity",
            ["lastName"] = "Identity",
            ["accountName"] = "Identity",
            ["designation"] = "Identity",
            ["phone"] = "Reach",
            ["phone2"] = "Reach",
            ["email"] = "Reach",
            ["whatsAppNumber"] = "Reach",
            ["type"] = "Classification",
            ["lifecycleStage"] = "Classification",
            ["source"] = "Classification",
            ["segment"] = "Classification",
            ["tags"] = "Classification",
            ["lifetimeValue"] = "Commercial",
            ["dealCount"] = "Commercial",
            ["budgetMin"] = "Commercial",
            ["budgetMax"] = "Commercial",
            ["preferredConfiguration"] = "Requirement",
            ["preferredLocality"] = "Requirement",
            ["doNotCall"] = "Consent",
            ["doNotEmail"] = "Consent",
            ["whatsAppOptIn"] = "Consent",
        }));
    }

    [PermissionAction(ObjectAction.View)]
    [HttpPost("query")]
    public async Task<ActionResult<PagedResult<ContactDto>>> Query(QueryRequest request, CancellationToken ct)
    {
        var result = await RunQueryAsync(
            Base(),
            request,
            Fields,
            c => ToDto(c),
            defaultSortPath: "UpdatedAt",
            aggregates: async filtered => new Dictionary<string, decimal>
            {
                ["lifetimeValue"] = await filtered.SumAsync(c => c.LifetimeValue, ct),
                ["deals"] = await filtered.SumAsync(c => c.DealCount, ct),
                ["customers"] = await filtered.CountAsync(c => c.Type == ContactTypes.Customer, ct),
            },
            cancellationToken: ct);

        // One extra query to decorate the page with live opportunity counts,
        // rather than a correlated subquery per row.
        var ids = result.Items.Select(i => i.Id).ToList();
        var openCounts = await Db.Opportunities
            .Where(o => o.ContactId != null && ids.Contains(o.ContactId.Value))
            .Where(o => o.Stage != OpportunityStages.ClosedWon && o.Stage != OpportunityStages.ClosedLost)
            .GroupBy(o => o.ContactId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);

        result.Items = result.Items
            .Select(i => i with { OpenOpportunities = openCounts.GetValueOrDefault(i.Id) })
            .ToList();

        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContactDto>>> GetAll(CancellationToken ct)
    {
        var contacts = await Base().OrderByDescending(c => c.UpdatedAt).Take(500).ToListAsync(ct);
        return Ok(contacts.Select(c => ToDto(c)).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ContactDto>> GetOne(int id, CancellationToken ct)
    {
        var contact = await Base().FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw ApiException.NotFound("Contact");

        var open = await Db.Opportunities.CountAsync(
            o => o.ContactId == id && OpportunityStages.Open.Contains(o.Stage), ct);

        return Ok(ToDto(contact, open));
    }

    /// <summary>Everything that has touched this contact, newest first.</summary>
    [HttpGet("{id:int}/timeline")]
    public async Task<ActionResult<IReadOnlyList<TimelineEntryDto>>> GetTimeline(int id, CancellationToken ct)
    {
        if (!await Db.Contacts.AnyAsync(c => c.Id == id, ct))
        {
            throw ApiException.NotFound("Contact");
        }

        var calls = await Db.CallLogs
            .Where(c => c.RelatedType == RelatedTypes.Contact && c.RelatedId == id)
            .Select(c => new TimelineEntryDto(
                "Call", c.Direction + " call — " + c.Outcome, c.Notes, c.AgentName, c.StartedAt))
            .ToListAsync(ct);

        var visits = await Db.SiteVisits
            .Where(v => v.ContactId == id)
            .Select(v => new TimelineEntryDto(
                "SiteVisit", v.VisitType + " — " + v.Status, v.Feedback, v.HostName ?? "—", v.ScheduledAt))
            .ToListAsync(ct);

        var quotes = await Db.Quotations
            .Where(q => q.ContactId == id)
            .Select(q => new TimelineEntryDto(
                "Quotation", q.QuoteNumber + " — " + q.Status, q.Title, q.Owner!.Name, q.IssueDate))
            .ToListAsync(ct);

        var followUps = await Db.FollowUps
            .Where(f => f.RelatedType == RelatedTypes.Contact && f.RelatedId == id)
            .Select(f => new TimelineEntryDto(
                "FollowUp", f.Subject, f.Outcome, f.Owner!.Name, f.DueAt))
            .ToListAsync(ct);

        return Ok(calls
            .Concat(visits).Concat(quotes).Concat(followUps)
            .OrderByDescending(e => e.At)
            .Take(100)
            .ToList());
    }

    /* ------------------------------------------------------------------ *
     * Writes
     * ------------------------------------------------------------------ */

    [HttpPost]
    public async Task<ActionResult<ContactDto>> Create(ContactInput input, CancellationToken ct)
    {
        var branch = await RequireBranchAsync(input.BranchId, ct);
        await RequireOwnerAsync(input.OwnerId, ct);

        var contact = new Contact
        {
            CompanyId = Db.Tenant.CompanyId,
            BranchId = branch.Id,
            OwnerId = input.OwnerId,
        };

        Apply(contact, input);

        Db.Contacts.Add(contact);
        await Db.SaveChangesAsync(ct);

        contact.Branch = branch;
        return CreatedAtAction(nameof(GetOne), new { id = contact.Id }, ToDto(contact));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ContactDto>> Update(int id, ContactInput input, CancellationToken ct)
    {
        var contact = await Db.Contacts.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw ApiException.NotFound("Contact");

        var branch = await RequireBranchAsync(input.BranchId, ct);
        await RequireOwnerAsync(input.OwnerId, ct);

        contact.BranchId = branch.Id;
        contact.OwnerId = input.OwnerId;
        Apply(contact, input);

        await Db.SaveChangesAsync(ct);

        contact.Branch = branch;
        return Ok(ToDto(contact));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var contact = await Db.Contacts.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw ApiException.NotFound("Contact");

        var openDeals = await Db.Opportunities.CountAsync(
            o => o.ContactId == id && OpportunityStages.Open.Contains(o.Stage), ct);

        if (openDeals > 0)
        {
            throw ApiException.Conflict(
                $"This contact has {openDeals} open opportunit{(openDeals == 1 ? "y" : "ies")}. " +
                "Close or reassign them first.");
        }

        SoftDelete(contact);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static void Apply(Contact contact, ContactInput input)
    {
        contact.Salutation = input.Salutation;
        contact.FirstName = input.FirstName.Trim();
        contact.LastName = input.LastName?.Trim();
        contact.FullName = string.Join(' ',
            new[] { contact.FirstName, contact.LastName }
                .Where(p => !string.IsNullOrWhiteSpace(p)));
        contact.Designation = input.Designation;
        contact.AccountName = input.AccountName;
        contact.Phone = input.Phone;
        contact.Phone2 = input.Phone2;
        contact.Email = input.Email;
        contact.WhatsAppNumber = input.WhatsAppNumber;
        contact.Address = input.Address;
        contact.City = input.City;
        contact.State = input.State;
        contact.Country = input.Country;
        contact.Pincode = input.Pincode;
        contact.Type = Require(input.Type, ContactTypes.All, "contact type");
        contact.LifecycleStage = Require(input.LifecycleStage, LifecycleStages.All, "lifecycle stage");
        contact.Source = Require(input.Source, LeadSources.All, "source");
        contact.Tags = input.Tags;
        contact.BudgetMin = input.BudgetMin;
        contact.BudgetMax = input.BudgetMax;
        contact.PreferredConfiguration = input.PreferredConfiguration;
        contact.PreferredLocality = input.PreferredLocality;
        contact.DoNotCall = input.DoNotCall;
        contact.DoNotEmail = input.DoNotEmail;
        contact.WhatsAppOptIn = input.WhatsAppOptIn;
        contact.PanNumber = input.PanNumber;
        contact.Gstin = input.Gstin;
        contact.DateOfBirth = input.DateOfBirth;
    }
}

public record TimelineEntryDto(string Type, string Title, string? Detail, string Actor, DateTime At);
