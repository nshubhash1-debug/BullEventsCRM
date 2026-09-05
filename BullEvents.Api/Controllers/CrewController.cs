using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// The roster: everyone who can work an event, on the payroll or not.
///
/// The question is the props question again, minus the arithmetic. A prop asks
/// how many of forty are free; a person is one, so the answer is simply whether
/// a blocking assignment overlaps the dates. Everything else here exists to
/// make the real request easy — <i>find me four bearers and a supervisor for
/// Friday</i> — which is why <see cref="Requisition"/> books a group in one
/// action rather than making a coordinator add five rows by hand.
/// </summary>
[ApiController]
[Route("api/crew")]
[Authorize]
[SecuredBy(SecuredObjects.Employee)]
public class CrewController(AppDbContext db) : CrmControllerBase(db)
{
    private static readonly FieldMap<CrewMember> Fields = new FieldMap<CrewMember>()
        .Text("name", "Name", searchable: true)
        .Text("code", "Code", searchable: true)
        .Select("primaryRole", "Role")
        .Text("secondaryRoles", "Also covers", searchable: true)
        .Select("engagementType", "Engagement")
        .Select("status", "Status")
        .Text("phone", "Phone", searchable: true)
        .Select("city", "City")
        .Bool("willTravel", "Will travel")
        .Number("dayRate", "Day rate")
        .Number("yearsExperience", "Experience")
        .Number("rating", "Rating")
        .Number("eventsWorked", "Events worked")
        .Number("noShowCount", "No-shows")
        .Select("supplierVendorName", "Contractor", "SupplierVendor.Name")
        .Date("createdAt", "Added");

    private IQueryable<CrewMember> Base() => Db.CrewMembers
        .Include(c => c.Employee)
        .Include(c => c.SupplierVendor)
        .AsNoTracking();

    /* ------------------------------------------------------------------ *
     * Roster
     * ------------------------------------------------------------------ */

    [HttpGet("fields")]
    public ActionResult<IReadOnlyList<FilterFieldDto>> FilterFields() =>
        Ok(DescribeFields(Fields, new Dictionary<string, IReadOnlyList<FilterOptionDto>>
        {
            ["primaryRole"] = Options(CrewRoles.All),
            ["engagementType"] = Options(CrewEngagementTypes.All),
            ["status"] = Options(CrewStatuses.All),
        }));

    [HttpGet("meta")]
    public ActionResult<object> Meta() => Ok(new
    {
        roles = CrewRoles.All,
        engagementTypes = CrewEngagementTypes.All,
        statuses = CrewStatuses.All,
        assignmentStatuses = CrewAssignmentStatuses.All,
    });

    [HttpPost("query")]
    public async Task<ActionResult<PagedResult<CrewMemberDto>>> Query(
        QueryRequest request, CancellationToken ct)
    {
        return Ok(await RunQueryAsync(
            Base(), request, Fields, c => ToDto(c),
            defaultSortPath: nameof(CrewMember.Name), defaultSortDescending: false,
            cancellationToken: ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CrewMemberDto>> Get(int id, CancellationToken ct)
    {
        var member = await Base().FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw ApiException.NotFound("Crew member");

        return Ok(ToDto(member));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost]
    public async Task<ActionResult<CrewMemberDto>> Create(
        CrewMemberInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw ApiException.BadRequest("A name is required.");

        var code = string.IsNullOrWhiteSpace(input.Code)
            ? await NextCodeAsync(ct)
            : input.Code.Trim().ToUpperInvariant();

        if (await Db.CrewMembers.AnyAsync(c => c.Code == code, ct))
            throw ApiException.Conflict($"Crew code {code} is already in use.");

        var member = new CrewMember
        {
            CompanyId = Db.Tenant.CompanyId,
            Name = input.Name.Trim(),
            Code = code,
            EngagementType = Require(input.EngagementType, CrewEngagementTypes.All, "engagement"),
            Status = Require(input.Status, CrewStatuses.All, "status"),
            PrimaryRole = Require(input.PrimaryRole, CrewRoles.All, "role"),
            SecondaryRoles = JoinRoles(input.SecondaryRoles),
            YearsExperience = input.YearsExperience,
            Phone = input.Phone,
            AltPhone = input.AltPhone,
            Email = input.Email,
            Address = input.Address,
            City = input.City,
            PhotoUrl = input.PhotoUrl,
            WillTravel = input.WillTravel,
            DayRate = input.DayRate,
            OvertimeHourlyRate = input.OvertimeHourlyRate,
            IdProofType = input.IdProofType,
            IdProofNumber = input.IdProofNumber,
            Notes = input.Notes,
            OwnerId = input.OwnerId ?? (Db.Tenant.UserId > 0 ? Db.Tenant.UserId : null),
        };

        await LinkRecordsAsync(member, input, ct);
        await RequireOwnerAsync(member.OwnerId, ct);

        Db.CrewMembers.Add(member);
        await Db.SaveChangesAsync(ct);

        return Ok(await GetDtoAsync(member.Id, ct));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<CrewMemberDto>> Update(
        int id, CrewMemberInput input, CancellationToken ct)
    {
        var member = await Db.CrewMembers.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw ApiException.NotFound("Crew member");

        member.Name = input.Name.Trim();
        member.EngagementType = Require(input.EngagementType, CrewEngagementTypes.All, "engagement");
        member.Status = Require(input.Status, CrewStatuses.All, "status");
        member.PrimaryRole = Require(input.PrimaryRole, CrewRoles.All, "role");
        member.SecondaryRoles = JoinRoles(input.SecondaryRoles);
        member.YearsExperience = input.YearsExperience;
        member.Phone = input.Phone;
        member.AltPhone = input.AltPhone;
        member.Email = input.Email;
        member.Address = input.Address;
        member.City = input.City;
        member.PhotoUrl = input.PhotoUrl;
        member.WillTravel = input.WillTravel;
        member.DayRate = input.DayRate;
        member.OvertimeHourlyRate = input.OvertimeHourlyRate;
        member.IdProofType = input.IdProofType;
        member.IdProofNumber = input.IdProofNumber;
        member.Notes = input.Notes;

        await LinkRecordsAsync(member, input, ct);

        if (input.OwnerId != member.OwnerId)
        {
            await RequireOwnerAsync(input.OwnerId, ct);
            member.OwnerId = input.OwnerId;
        }

        await Db.SaveChangesAsync(ct);
        return Ok(await GetDtoAsync(member.Id, ct));
    }

    [PermissionAction(ObjectAction.Delete)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var member = await Db.CrewMembers.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw ApiException.NotFound("Crew member");

        var booked = await Db.CrewAssignments.AnyAsync(
            a => a.CrewMemberId == id
                && (a.Status == CrewAssignmentStatuses.Planned
                    || a.Status == CrewAssignmentStatuses.Confirmed), ct);

        if (booked)
            throw ApiException.BadRequest(
                "This person is booked on an upcoming event. Release those assignments first.");

        SoftDelete(member);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static string? JoinRoles(IReadOnlyList<string>? roles)
    {
        if (roles is null or { Count: 0 }) return null;

        foreach (var role in roles)
        {
            if (!CrewRoles.All.Contains(role))
                throw ApiException.BadRequest($"'{role}' is not a valid role.");
        }

        return string.Join(",", roles.Distinct());
    }

    private async Task LinkRecordsAsync(
        CrewMember member, CrewMemberInput input, CancellationToken ct)
    {
        if (input.EmployeeId is int employeeId)
        {
            var exists = await Db.HrEmployees.AnyAsync(e => e.Id == employeeId, ct);
            if (!exists) throw ApiException.BadRequest("Select a valid employee.");
        }

        if (input.SupplierVendorId is int vendorId)
        {
            var exists = await Db.Vendors.AnyAsync(v => v.Id == vendorId, ct);
            if (!exists) throw ApiException.BadRequest("Select a valid labour contractor.");
        }

        member.EmployeeId = input.EmployeeId;
        member.SupplierVendorId = input.SupplierVendorId;
    }

    /* ------------------------------------------------------------------ *
     * Availability
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Which people already have a blocking assignment across a window.
    ///
    /// Returns the assignments themselves rather than a count, because the
    /// picker wants to say <i>why</i> somebody is unavailable — "on the Kapoor
    /// mehendi, 12th to 14th" is useful; "unavailable" is not.
    /// </summary>
    private async Task<Dictionary<int, List<CrewAssignment>>> ClashesAsync(
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<int>? crewIds,
        int? excludeAssignmentId,
        CancellationToken ct)
    {
        var query = Db.CrewAssignments.AsNoTracking()
            .Where(a => CrewAssignmentStatuses.Blocking.Contains(a.Status))
            .Where(a => a.FromDate <= to && a.ToDate >= from);

        if (crewIds is { Count: > 0 })
            query = query.Where(a => crewIds.Contains(a.CrewMemberId));

        if (excludeAssignmentId is int exclude)
            query = query.Where(a => a.Id != exclude);

        var rows = await query.ToListAsync(ct);

        return rows.GroupBy(a => a.CrewMemberId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    [HttpPost("availability")]
    public async Task<ActionResult<IReadOnlyList<CrewMemberDto>>> Availability(
        CrewAvailabilityRequest request, CancellationToken ct)
    {
        if (request.To < request.From)
            throw ApiException.BadRequest("The end date cannot fall before the start date.");

        var query = Base().Where(c => c.Status == CrewStatuses.Active);

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var role = request.Role.Trim();

            // Wrapped in separators, folded to lower and re-collated to "C":
            // every string column carries the app's non-deterministic ICU
            // collation and Postgres will not run LIKE against one. Same fix as
            // QueryEngine.CaseInsensitiveText.
            var needle = "," + role.ToLowerInvariant() + ",";

            // Primary or secondary — somebody listed as a helper who also does
            // lights should turn up when a light technician is wanted.
            query = query.Where(c =>
                c.PrimaryRole == role
                || (c.SecondaryRoles != null
                    && EF.Functions.Collate(("," + c.SecondaryRoles + ",").ToLower(), "C")
                        .Contains(needle)));
        }

        if (!string.IsNullOrWhiteSpace(request.EngagementType))
            query = query.Where(c => c.EngagementType == request.EngagementType);

        if (!string.IsNullOrWhiteSpace(request.City))
        {
            var city = request.City.Trim();
            query = query.Where(c => c.City == city || c.WillTravel);
        }

        var members = await query.OrderBy(c => c.Name).Take(500).ToListAsync(ct);
        var ids = members.Select(c => c.Id).ToList();
        var clashes = await ClashesAsync(
            request.From, request.To, ids, request.ExcludeAssignmentId, ct);

        var rows = members
            .Select(c => ToDto(c, (clashes.GetValueOrDefault(c.Id) ?? []).Count))
            .Where(c => !request.AvailableOnly || c.IsAvailable == true)
            // Free first, then the ones who turn up: rating high, no-shows low.
            .OrderByDescending(c => c.IsAvailable == true)
            .ThenBy(c => c.NoShowCount)
            .ThenByDescending(c => c.Rating ?? 0)
            .ThenBy(c => c.Name)
            .ToList();

        return Ok(rows);
    }

    /// <summary>
    /// How many of each trade can be fielded over a window.
    ///
    /// The number a coordinator needs before promising a client anything: not
    /// who is free, but whether eight bearers exist at all on that Saturday.
    /// </summary>
    [HttpPost("coverage")]
    public async Task<ActionResult<IReadOnlyList<CrewRoleCoverageDto>>> Coverage(
        CrewAvailabilityRequest request, CancellationToken ct)
    {
        if (request.To < request.From)
            throw ApiException.BadRequest("The end date cannot fall before the start date.");

        var members = await Base()
            .Where(c => c.Status == CrewStatuses.Active)
            .ToListAsync(ct);

        var clashes = await ClashesAsync(
            request.From, request.To, members.Select(c => c.Id).ToList(), null, ct);

        var rows = CrewRoles.All
            .Select(role =>
            {
                var canWork = members.Where(m => m.CanWork(role)).ToList();
                var free = canWork.Count(m => !clashes.ContainsKey(m.Id));
                var rates = canWork.Where(m => m.DayRate > 0).Select(m => m.DayRate!.Value).ToList();

                return new CrewRoleCoverageDto(
                    role, canWork.Count, free, canWork.Count - free,
                    rates.Count == 0 ? null : Math.Round(rates.Average(), 0));
            })
            .Where(r => r.OnRoster > 0)
            .OrderByDescending(r => r.OnRoster)
            .ToList();

        return Ok(rows);
    }

    /* ------------------------------------------------------------------ *
     * Assignments
     * ------------------------------------------------------------------ */

    private IQueryable<CrewAssignment> AssignmentBase() => Db.CrewAssignments
        .Include(a => a.CrewMember)
        .AsNoTracking();

    [HttpGet("assignments")]
    public async Task<ActionResult<IReadOnlyList<CrewAssignmentDto>>> Assignments(
        [FromQuery] string? status,
        [FromQuery] int? crewMemberId,
        [FromQuery] int? leadId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] bool unpaidOnly = false,
        [FromQuery] int take = 200,
        CancellationToken ct = default)
    {
        var query = AssignmentBase();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(a => a.Status == status);
        if (crewMemberId is int cid) query = query.Where(a => a.CrewMemberId == cid);
        if (leadId is int lid) query = query.Where(a => a.LeadId == lid);
        if (from is DateOnly f) query = query.Where(a => a.ToDate >= f);
        if (to is DateOnly t) query = query.Where(a => a.FromDate <= t);

        if (unpaidOnly)
        {
            query = query.Where(a => !a.IsPaid
                && a.Status == CrewAssignmentStatuses.Completed);
        }

        var rows = await query
            .OrderByDescending(a => a.FromDate).ThenBy(a => a.Id)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(ct);

        return Ok(rows.Select(ToDto).ToList());
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("assignments")]
    public async Task<ActionResult<CrewAssignmentDto>> Assign(
        CrewAssignmentInput input, CancellationToken ct)
    {
        var assignment = await BuildAssignmentAsync(input, ct);

        Db.CrewAssignments.Add(assignment);
        await Db.SaveChangesAsync(ct);

        return Ok(await GetAssignmentDtoAsync(assignment.Id, ct));
    }

    /// <summary>
    /// Books several people onto one event at once.
    ///
    /// Every candidate is checked before any row is written, so a requisition
    /// that cannot be fully met fails whole rather than leaving three of five
    /// bearers booked and the coordinator guessing which.
    /// </summary>
    [PermissionAction(ObjectAction.Create)]
    [HttpPost("requisition")]
    public async Task<ActionResult<IReadOnlyList<CrewAssignmentDto>>> Requisition(
        CrewRequisitionInput input, CancellationToken ct)
    {
        if (input.CrewMemberIds.Count == 0)
            throw ApiException.BadRequest("Pick at least one person.");

        if (input.ToDate < input.FromDate)
            throw ApiException.BadRequest("The end date cannot fall before the start date.");

        var created = new List<CrewAssignment>();

        foreach (var crewId in input.CrewMemberIds.Distinct())
        {
            var assignment = await BuildAssignmentAsync(new CrewAssignmentInput(
                CrewMemberId: crewId,
                FromDate: input.FromDate,
                ToDate: input.ToDate,
                Role: input.Role,
                LeadId: input.LeadId,
                ProjectId: input.ProjectId,
                PropIssueId: input.PropIssueId,
                EventName: input.EventName,
                ClientName: input.ClientName,
                VenueName: input.VenueName,
                ReportingTime: input.ReportingTime), ct);

            Db.CrewAssignments.Add(assignment);
            created.Add(assignment);
        }

        await Db.SaveChangesAsync(ct);

        await LogLeadActivityAsync(
            input.LeadId, LeadActivityTypes.Note,
            $"{created.Count} crew booked as {CrmControllerBase.Humanise(input.Role).ToLowerInvariant()} " +
            $"for {input.FromDate:dd MMM} to {input.ToDate:dd MMM yyyy}.", ct);

        await Db.SaveChangesAsync(ct);

        var ids = created.Select(a => a.Id).ToList();
        var rows = await AssignmentBase().Where(a => ids.Contains(a.Id)).ToListAsync(ct);

        return Ok(rows.Select(ToDto).ToList());
    }

    private async Task<CrewAssignment> BuildAssignmentAsync(
        CrewAssignmentInput input, CancellationToken ct)
    {
        var member = await Db.CrewMembers.FirstOrDefaultAsync(c => c.Id == input.CrewMemberId, ct)
            ?? throw ApiException.BadRequest($"Crew member #{input.CrewMemberId} does not exist.");

        if (!member.IsBookable)
            throw ApiException.BadRequest(
                $"{member.Name} is {member.Status.ToLowerInvariant()} and cannot be booked.");

        var to = input.ToDate ?? input.FromDate;
        if (to < input.FromDate)
            throw ApiException.BadRequest("The end date cannot fall before the start date.");

        var role = Require(input.Role, CrewRoles.All, "role");

        var clashes = await ClashesAsync(input.FromDate, to, [member.Id], null, ct);
        if (clashes.TryGetValue(member.Id, out var existing) && existing.Count > 0)
        {
            var clash = existing[0];
            throw ApiException.BadRequest(
                $"{member.Name} is already booked {clash.FromDate:dd MMM} to {clash.ToDate:dd MMM}" +
                (clash.EventName is null ? "." : $" on {clash.EventName}."));
        }

        return new CrewAssignment
        {
            CompanyId = Db.Tenant.CompanyId,
            CrewMemberId = member.Id,
            Status = CrewAssignmentStatuses.Planned,
            Role = role,
            LeadId = input.LeadId,
            BookingId = input.BookingId,
            ProjectId = input.ProjectId,
            PropIssueId = input.PropIssueId,
            EventName = input.EventName,
            ClientName = input.ClientName,
            VenueName = input.VenueName,
            FromDate = input.FromDate,
            ToDate = to,
            ReportingTime = input.ReportingTime,
            ClosingTime = input.ClosingTime,
            // Falls back to the person's standard rate, which is what makes a
            // requisition of five people cost itself without five more inputs.
            DayRate = input.DayRate ?? member.DayRate,
            AllowanceAmount = input.AllowanceAmount,
            Notes = input.Notes,
        };
    }

    /// <summary>
    /// Moves an assignment along, and keeps the person's record honest as it
    /// goes — a completed job raises their count, a no-show raises the number a
    /// coordinator actually reads.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("assignments/{id:int}/status")]
    public async Task<ActionResult<CrewAssignmentDto>> SetAssignmentStatus(
        int id,
        [FromQuery] string status,
        [FromQuery] int? rating,
        [FromQuery] decimal? overtimeHours,
        CancellationToken ct)
    {
        var assignment = await Db.CrewAssignments
            .Include(a => a.CrewMember)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw ApiException.NotFound("Assignment");

        var target = Require(status, CrewAssignmentStatuses.All, "status");
        var member = assignment.CrewMember;

        if (assignment.Status == target)
            throw ApiException.BadRequest($"This is already {target.ToLowerInvariant()}.");

        assignment.Status = target;

        if (overtimeHours is decimal hours && hours > 0)
        {
            assignment.OvertimeHours = hours;
            assignment.OvertimeAmount = hours * (member?.OvertimeHourlyRate ?? 0);
        }

        if (member is not null)
        {
            switch (target)
            {
                case CrewAssignmentStatuses.Completed:
                    member.EventsWorked += 1;
                    if (rating is int score)
                    {
                        assignment.Rating = Math.Clamp(score, 1, 5);
                        var previous = (member.Rating ?? 0) * (member.EventsWorked - 1);
                        member.Rating = Math.Round(
                            (previous + assignment.Rating.Value) / member.EventsWorked, 2);
                    }
                    break;

                case CrewAssignmentStatuses.NoShow:
                    member.NoShowCount += 1;
                    break;
            }
        }

        await Db.SaveChangesAsync(ct);
        return Ok(await GetAssignmentDtoAsync(assignment.Id, ct));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("assignments/{id:int}/pay")]
    public async Task<ActionResult<CrewAssignmentDto>> MarkPaid(
        int id, [FromQuery] DateOnly? paidOn, CancellationToken ct)
    {
        var assignment = await Db.CrewAssignments.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw ApiException.NotFound("Assignment");

        if (assignment.Status != CrewAssignmentStatuses.Completed)
            throw ApiException.BadRequest("Only a completed assignment can be paid.");

        assignment.IsPaid = true;
        assignment.PaidOn = paidOn ?? DateOnly.FromDateTime(DateTime.UtcNow);

        await Db.SaveChangesAsync(ct);
        return Ok(await GetAssignmentDtoAsync(assignment.Id, ct));
    }

    [PermissionAction(ObjectAction.Delete)]
    [HttpDelete("assignments/{id:int}")]
    public async Task<IActionResult> RemoveAssignment(int id, CancellationToken ct)
    {
        var assignment = await Db.CrewAssignments.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw ApiException.NotFound("Assignment");

        if (assignment.Status == CrewAssignmentStatuses.Completed)
            throw ApiException.BadRequest(
                "This assignment was worked. Its record stays for the payment history.");

        Db.CrewAssignments.Remove(assignment);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    /* ------------------------------------------------------------------ *
     * Projection
     * ------------------------------------------------------------------ */

    private async Task<CrewMemberDto> GetDtoAsync(int id, CancellationToken ct)
    {
        var member = await Base().FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw ApiException.NotFound("Crew member");

        return ToDto(member);
    }

    private async Task<CrewAssignmentDto> GetAssignmentDtoAsync(int id, CancellationToken ct)
    {
        var assignment = await AssignmentBase().FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw ApiException.NotFound("Assignment");

        return ToDto(assignment);
    }

    private async Task<string> NextCodeAsync(CancellationToken ct)
    {
        var count = await Db.CrewMembers.IgnoreQueryFilters()
            .CountAsync(c => c.CompanyId == Db.Tenant.CompanyId, ct);

        for (var attempt = 0; attempt < 50; attempt++)
        {
            var code = $"CR-{count + 1 + attempt:D4}";
            if (!await Db.CrewMembers.AnyAsync(c => c.Code == code, ct)) return code;
        }

        return Code("CR");
    }

    internal static CrewMemberDto ToDto(CrewMember member, int? clashes = null)
    {
        var secondary = string.IsNullOrWhiteSpace(member.SecondaryRoles)
            ? []
            : member.SecondaryRoles
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList();

        return new CrewMemberDto(
            member.Id, member.Name, member.Code, member.EngagementType, member.Status,
            member.EmployeeId, member.Employee?.Name,
            member.SupplierVendorId, member.SupplierVendor?.Name,
            member.PrimaryRole, secondary, member.YearsExperience,
            member.Phone, member.AltPhone, member.Email, member.Address, member.City,
            member.PhotoUrl, member.WillTravel,
            member.DayRate, member.OvertimeHourlyRate,
            member.Rating, member.EventsWorked, member.NoShowCount,
            member.IdProofType, member.IdProofNumber, member.Notes, member.OwnerId,
            clashes,
            clashes is null ? null : clashes == 0,
            member.CreatedAt, member.UpdatedAt);
    }

    internal static CrewAssignmentDto ToDto(CrewAssignment a) => new(
        a.Id, a.CrewMemberId, a.CrewMember?.Name ?? "", a.CrewMember?.Code ?? "",
        a.CrewMember?.Phone, a.CrewMember?.EngagementType ?? "",
        a.Status, a.Role,
        a.LeadId, a.BookingId, a.ProjectId, a.PropIssueId,
        a.EventName, a.ClientName, a.VenueName,
        a.FromDate, a.ToDate, a.ReportingTime, a.ClosingTime,
        a.DayRate, a.OvertimeHours, a.OvertimeAmount, a.AllowanceAmount, a.TotalCost,
        a.IsPaid, a.PaidOn, a.Rating, a.Notes, a.CreatedAt);
}
