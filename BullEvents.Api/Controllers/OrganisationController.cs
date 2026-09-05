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

public record CompanyProfileDto(
    int Id,
    string Name,
    string? LegalName,
    string Slug,
    string? Cin,
    string? Pan,
    string? Tan,
    string? ReraNumber,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? Pincode,
    string? Country,
    string? PostalAddress,
    string? Phone,
    string? Email,
    string? SupportEmail,
    string? Website,
    int FinancialYearStartMonth,
    string CurrencyCode,
    string TimeZoneId,
    string? BrandColor,
    string? LogoUrl,
    string? LetterheadFooter,
    string PlanTier,
    string Status,
    int Branches,
    int ActiveUsers,
    int Teams,
    /// <summary>What a letterhead still cannot print. Empty means the profile is complete.</summary>
    IReadOnlyList<string> Missing);

public record SaveCompanyProfileRequest(
    [Required] string Name,
    string? LegalName,
    string? Cin,
    string? Pan,
    string? Tan,
    string? ReraNumber,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? Pincode,
    string? Country,
    string? Phone,
    [EmailAddress] string? Email,
    [EmailAddress] string? SupportEmail,
    string? Website,
    [Range(1, 12)] int FinancialYearStartMonth,
    string? CurrencyCode,
    string? TimeZoneId,
    string? BrandColor,
    string? LogoUrl,
    string? LetterheadFooter);

public record DesignationDto(
    int Id,
    string Name,
    string? Code,
    int Level,
    string? Description,
    string? SuggestedRole,
    bool IsActive,
    /// <summary>Live accounts holding this title. Retiring one with holders is refused.</summary>
    int Holders);

public record SaveDesignationRequest(
    [Required] string Name,
    string? Code,
    [Range(1, 20)] int Level,
    string? Description,
    string? SuggestedRole,
    bool IsActive = true);

public record TeamRosterDto(
    int Id,
    int UserId,
    string UserName,
    string UserEmail,
    string UserRole,
    string? DesignationName,
    string RoleInTeam,
    DateTime JoinedOn,
    DateTime? LeftOn,
    bool IsPrimary,
    string? Notes);

public record TeamDto(
    int Id,
    string Name,
    string? Code,
    string Kind,
    string KindLabel,
    int? BranchId,
    string? BranchName,
    int? LeadUserId,
    string? LeadUserName,
    int? ParentTeamId,
    string? ParentTeamName,
    string? Description,
    bool IsActive,
    int MemberCount,
    IReadOnlyList<TeamRosterDto> Members);

public record SaveTeamRequest(
    [Required] string Name,
    string? Code,
    [Required] string Kind,
    int? BranchId,
    int? LeadUserId,
    int? ParentTeamId,
    string? Description,
    bool IsActive = true);

public record AddTeamMemberRequest(
    [Required] int UserId,
    string? RoleInTeam,
    DateTime? JoinedOn,
    bool IsPrimary = true,
    string? Notes = null);

public record RemoveTeamMemberRequest(DateTime? LeftOn, string? Reason);

public record TransferMemberRequest(
    [Required] int UserId,
    [Required] int FromTeamId,
    [Required] int ToTeamId,
    DateTime? EffectiveOn,
    string? Reason,
    string? RoleInTeam);

public record TeamTransferDto(
    int Id,
    int UserId,
    string UserName,
    int? FromTeamId,
    string? FromTeamName,
    int? ToTeamId,
    string? ToTeamName,
    DateTime EffectiveOn,
    string? Reason,
    string? MovedByName,
    /// <summary>Joined, moved, or left — what the two ends actually mean.</summary>
    string Kind);

/* ------------------------------------------------------------------ *
 * Controller
 * ------------------------------------------------------------------ */

/// <summary>
/// The company's own record of itself: its legal identity, the titles it uses,
/// and the desks people sit at.
///
/// Kept together because they answer one question between them — "who is this
/// company and how is it arranged" — and an administrator setting up a tenant
/// works through all three in one sitting.
/// </summary>
[ApiController]
[Route("api/organisation")]
[Authorize]
[RequireModule(Modules.Administration)]
public class OrganisationController(AppDbContext db, TenantContext tenant, TeamService teams)
    : CrmControllerBase(db)
{
    /* ------------------------------------------------------------------ *
     * Company profile
     * ------------------------------------------------------------------ */

    [HttpGet("company")]
    public async Task<CompanyProfileDto> Company(CancellationToken ct)
        => await Project(await CompanyRow(ct), ct);

    [HttpPut("company")]
    public async Task<CompanyProfileDto> SaveCompany(
        SaveCompanyProfileRequest request, CancellationToken ct)
    {
        RequireAdmin();

        var company = await CompanyRow(ct);

        company.Name = request.Name.Trim();
        company.LegalName = Trim(request.LegalName);
        company.Cin = Upper(request.Cin);
        company.Pan = Upper(request.Pan);
        company.Tan = Upper(request.Tan);
        company.ReraNumber = Trim(request.ReraNumber);

        company.AddressLine1 = Trim(request.AddressLine1);
        company.AddressLine2 = Trim(request.AddressLine2);
        company.City = Trim(request.City);
        company.State = Trim(request.State);
        company.Pincode = Trim(request.Pincode);
        company.Country = Trim(request.Country) ?? "India";

        company.Phone = Trim(request.Phone);
        company.Email = Trim(request.Email);
        company.SupportEmail = Trim(request.SupportEmail);
        company.Website = Trim(request.Website);

        company.FinancialYearStartMonth = request.FinancialYearStartMonth;
        company.CurrencyCode = Trim(request.CurrencyCode) ?? "INR";
        company.TimeZoneId = Trim(request.TimeZoneId) ?? "India Standard Time";
        company.BrandColor = Trim(request.BrandColor);
        company.LogoUrl = Trim(request.LogoUrl);
        company.LetterheadFooter = Trim(request.LetterheadFooter);

        await Db.SaveChangesAsync(ct);

        return await Project(company, ct);
    }

    private async Task<Company> CompanyRow(CancellationToken ct) =>
        await Db.Companies.FirstOrDefaultAsync(c => c.Id == tenant.CompanyId, ct)
        ?? throw ApiException.NotFound("Company");

    private async Task<CompanyProfileDto> Project(Company c, CancellationToken ct)
    {
        // What a letterhead or an invoice still cannot print. Surfaced as a list
        // rather than a boolean, because "incomplete" is useless to an
        // administrator and "no CIN, no registered address" is actionable.
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(c.LegalName)) missing.Add("legal name");
        if (c.PostalAddress is null) missing.Add("registered address");
        if (string.IsNullOrWhiteSpace(c.Cin)) missing.Add("CIN");
        if (string.IsNullOrWhiteSpace(c.Pan)) missing.Add("PAN");
        if (string.IsNullOrWhiteSpace(c.Phone)) missing.Add("phone");
        if (string.IsNullOrWhiteSpace(c.Email)) missing.Add("email");

        return new CompanyProfileDto(
            c.Id,
            c.Name,
            c.LegalName,
            c.Slug,
            c.Cin,
            c.Pan,
            c.Tan,
            c.ReraNumber,
            c.AddressLine1,
            c.AddressLine2,
            c.City,
            c.State,
            c.Pincode,
            c.Country,
            c.PostalAddress,
            c.Phone,
            c.Email,
            c.SupportEmail,
            c.Website,
            c.FinancialYearStartMonth,
            c.CurrencyCode,
            c.TimeZoneId,
            c.BrandColor,
            c.LogoUrl,
            c.LetterheadFooter,
            c.PlanTier,
            c.Status,
            Branches: await Db.Branches.CountAsync(b => b.CompanyId == c.Id, ct),
            ActiveUsers: await Db.Users.CountAsync(u => u.CompanyId == c.Id && u.IsActive, ct),
            Teams: await Db.Set<Team>().CountAsync(t => t.IsActive, ct),
            Missing: missing);
    }

    /* ------------------------------------------------------------------ *
     * Designations
     * ------------------------------------------------------------------ */

    [HttpGet("designations")]
    public async Task<IReadOnlyList<DesignationDto>> Designations(
        [FromQuery] bool includeRetired, CancellationToken ct)
    {
        var rows = await Db.Set<Designation>()
            .Where(d => includeRetired || d.IsActive)
            .OrderBy(d => d.Level).ThenBy(d => d.Name)
            .Select(d => new
            {
                Designation = d,
                Holders = Db.Users.Count(u => u.DesignationId == d.Id && u.IsActive),
            })
            .ToListAsync(ct);

        return [.. rows.Select(r => new DesignationDto(
            r.Designation.Id,
            r.Designation.Name,
            r.Designation.Code,
            r.Designation.Level,
            r.Designation.Description,
            r.Designation.SuggestedRole,
            r.Designation.IsActive,
            r.Holders))];
    }

    [HttpPost("designations")]
    public Task<ActionResult<DesignationDto>> CreateDesignation(
        SaveDesignationRequest request, CancellationToken ct)
        => SaveDesignation(null, request, ct);

    [HttpPut("designations/{id:int}")]
    public Task<ActionResult<DesignationDto>> UpdateDesignation(
        int id, SaveDesignationRequest request, CancellationToken ct)
        => SaveDesignation(id, request, ct);

    private async Task<ActionResult<DesignationDto>> SaveDesignation(
        int? id, SaveDesignationRequest request, CancellationToken ct)
    {
        RequireAdmin();

        var name = request.Name.Trim();

        if (request.SuggestedRole is string role && !Roles.All.Contains(role))
        {
            throw ApiException.BadRequest($"'{role}' is not a role this system knows.");
        }

        var designation = id is int existing
            ? await Db.Set<Designation>().FirstOrDefaultAsync(d => d.Id == existing, ct)
                ?? throw ApiException.NotFound("Designation")
            : new Designation { CompanyId = tenant.CompanyId };

        // Collated as "C" because the database's own collation is
        // non-deterministic and will not compare equality the way a duplicate
        // check needs it to.
        var duplicate = await Db.Set<Designation>()
            .AnyAsync(d => d.Id != designation.Id
                && EF.Functions.Collate(d.Name, "C") == name, ct);

        if (duplicate)
        {
            throw ApiException.BadRequest($"There is already a designation called '{name}'.");
        }

        // Retiring a title somebody still holds would leave their profile
        // pointing at something the list no longer offers.
        if (!request.IsActive && designation.Id != 0)
        {
            var holders = await Db.Users
                .CountAsync(u => u.DesignationId == designation.Id && u.IsActive, ct);

            if (holders > 0)
            {
                throw ApiException.BadRequest(
                    $"{holders} active {(holders == 1 ? "person holds" : "people hold")} "
                    + $"'{designation.Name}'. Move them to another title first.");
            }
        }

        designation.Name = name;
        designation.Code = Trim(request.Code);
        designation.Level = request.Level;
        designation.Description = Trim(request.Description);
        designation.SuggestedRole = request.SuggestedRole;
        designation.IsActive = request.IsActive;
        designation.UpdatedAt = DateTime.UtcNow;

        if (designation.Id == 0) Db.Set<Designation>().Add(designation);

        await Db.SaveChangesAsync(ct);

        var rows = await Designations(includeRetired: true, ct);
        var row = rows.FirstOrDefault(r => r.Id == designation.Id);

        return row is null ? NotFound() : row;
    }

    /* ------------------------------------------------------------------ *
     * Teams
     * ------------------------------------------------------------------ */

    [HttpGet("teams")]
    public async Task<IReadOnlyList<TeamDto>> Teams(
        [FromQuery] bool includeRetired,
        [FromQuery] bool includePast,
        CancellationToken ct)
    {
        var rows = await Db.Set<Team>()
            .Where(t => includeRetired || t.IsActive)
            .OrderBy(t => t.Kind).ThenBy(t => t.Name)
            .Select(t => new
            {
                Team = t,
                BranchName = t.Branch != null ? t.Branch.Name : null,
                LeadUserName = t.LeadUser != null ? t.LeadUser.Name : null,
                ParentTeamName = t.ParentTeam != null ? t.ParentTeam.Name : null,
            })
            .ToListAsync(ct);

        var ids = rows.Select(r => r.Team.Id).ToList();

        var members = await Db.Set<TeamMember>()
            .Where(m => ids.Contains(m.TeamId) && (includePast || m.LeftOn == null))
            .OrderBy(m => m.LeftOn != null).ThenBy(m => m.RoleInTeam == TeamMemberRoles.Lead ? 0 : 1)
            .ThenBy(m => m.JoinedOn)
            .Select(m => new
            {
                Member = m,
                UserName = m.User!.Name,
                UserEmail = m.User.Email,
                UserRole = m.User.Role,
                DesignationName = m.User.Designation != null ? m.User.Designation.Name : null,
            })
            .ToListAsync(ct);

        var byTeam = members.GroupBy(m => m.Member.TeamId).ToDictionary(g => g.Key, g => g.ToList());

        return [.. rows.Select(r =>
        {
            var list = byTeam.TryGetValue(r.Team.Id, out var found) ? found : [];

            return new TeamDto(
                r.Team.Id,
                r.Team.Name,
                r.Team.Code,
                r.Team.Kind,
                TeamKinds.Label(r.Team.Kind),
                r.Team.BranchId,
                r.BranchName,
                r.Team.LeadUserId,
                r.LeadUserName,
                r.Team.ParentTeamId,
                r.ParentTeamName,
                r.Team.Description,
                r.Team.IsActive,
                MemberCount: list.Count(m => m.Member.LeftOn == null),
                Members: [.. list.Select(m => new TeamRosterDto(
                    m.Member.Id,
                    m.Member.UserId,
                    m.UserName,
                    m.UserEmail,
                    m.UserRole,
                    m.DesignationName,
                    m.Member.RoleInTeam,
                    m.Member.JoinedOn,
                    m.Member.LeftOn,
                    m.Member.IsPrimary,
                    m.Member.Notes))]);
        })];
    }

    [HttpPost("teams")]
    public Task<ActionResult<TeamDto>> CreateTeam(SaveTeamRequest request, CancellationToken ct)
        => SaveTeam(null, request, ct);

    [HttpPut("teams/{id:int}")]
    public Task<ActionResult<TeamDto>> UpdateTeam(
        int id, SaveTeamRequest request, CancellationToken ct)
        => SaveTeam(id, request, ct);

    private async Task<ActionResult<TeamDto>> SaveTeam(
        int? id, SaveTeamRequest request, CancellationToken ct)
    {
        RequireAdmin();

        var team = await teams.SaveAsync(
            id,
            request.Name.Trim(),
            Trim(request.Code),
            request.Kind,
            request.BranchId,
            request.LeadUserId,
            request.ParentTeamId,
            Trim(request.Description),
            request.IsActive,
            ct);

        return await OneTeam(team.Id, ct);
    }

    [HttpPost("teams/{id:int}/retire")]
    public async Task<ActionResult<TeamDto>> RetireTeam(int id, CancellationToken ct)
    {
        RequireAdmin();

        await teams.RetireAsync(id, ct);
        return await OneTeam(id, ct);
    }

    /* ---------------- mapping ---------------- */

    [HttpPost("teams/{id:int}/members")]
    public async Task<ActionResult<TeamDto>> AddMember(
        int id, AddTeamMemberRequest request, CancellationToken ct)
    {
        RequireAdmin();

        await teams.AddMemberAsync(
            id,
            request.UserId,
            request.RoleInTeam ?? TeamMemberRoles.Member,
            request.JoinedOn,
            request.IsPrimary,
            Trim(request.Notes),
            ct);

        return await OneTeam(id, ct);
    }

    [HttpPost("teams/{id:int}/members/{userId:int}/remove")]
    public async Task<ActionResult<TeamDto>> RemoveMember(
        int id, int userId, RemoveTeamMemberRequest request, CancellationToken ct)
    {
        RequireAdmin();

        await teams.RemoveMemberAsync(id, userId, request.LeftOn, Trim(request.Reason), ct);
        return await OneTeam(id, ct);
    }

    [HttpPost("teams/transfer")]
    public async Task<ActionResult<TeamDto>> Transfer(
        TransferMemberRequest request, CancellationToken ct)
    {
        RequireAdmin();

        await teams.TransferAsync(
            request.UserId,
            request.FromTeamId,
            request.ToTeamId,
            request.EffectiveOn,
            Trim(request.Reason),
            request.RoleInTeam,
            ct);

        return await OneTeam(request.ToTeamId, ct);
    }

    /// <summary>
    /// The movement log.
    ///
    /// Reads as a sentence rather than two ids: a null origin is somebody
    /// joining their first team and a null destination is somebody leaving
    /// without one, and calling all three "transfer" makes an arrivals report
    /// impossible to write.
    /// </summary>
    [HttpGet("teams/transfers")]
    public async Task<IReadOnlyList<TeamTransferDto>> Transfers(
        [FromQuery] int? userId, [FromQuery] int? teamId, CancellationToken ct)
    {
        var rows = await Db.Set<TeamTransfer>()
            .Where(t => (userId == null || t.UserId == userId)
                && (teamId == null || t.FromTeamId == teamId || t.ToTeamId == teamId))
            .OrderByDescending(t => t.EffectiveOn).ThenByDescending(t => t.Id)
            .Take(500)
            .Select(t => new
            {
                Transfer = t,
                UserName = t.User!.Name,
                FromTeamName = t.FromTeam != null ? t.FromTeam.Name : null,
                ToTeamName = t.ToTeam != null ? t.ToTeam.Name : null,
            })
            .ToListAsync(ct);

        return [.. rows.Select(r => new TeamTransferDto(
            r.Transfer.Id,
            r.Transfer.UserId,
            r.UserName,
            r.Transfer.FromTeamId,
            r.FromTeamName,
            r.Transfer.ToTeamId,
            r.ToTeamName,
            r.Transfer.EffectiveOn,
            r.Transfer.Reason,
            r.Transfer.MovedByName,
            Kind: r.Transfer.FromTeamId is null ? "Joined"
                : r.Transfer.ToTeamId is null ? "Left"
                : "Moved"))];
    }

    /// <summary>Everyone who could be put on a team, with where they already sit.</summary>
    [HttpGet("assignable")]
    public async Task<object> Assignable(CancellationToken ct)
    {
        var users = await Db.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.Name)
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.Email,
                u.Role,
                u.EmployeeCode,
                DesignationId = u.DesignationId,
                DesignationName = u.Designation != null ? u.Designation.Name : null,
                Teams = Db.Set<TeamMember>()
                    .Where(m => m.UserId == u.Id && m.LeftOn == null)
                    .Select(m => new { m.TeamId, TeamName = m.Team!.Name, m.IsPrimary })
                    .ToList(),
            })
            .ToListAsync(ct);

        var branches = await Db.Branches
            .OrderBy(b => b.Name)
            .Select(b => new { b.Id, b.Name, b.City })
            .ToListAsync(ct);

        return new
        {
            users,
            branches,
            teamKinds = TeamKinds.All.Select(k => new { value = k, label = TeamKinds.Label(k) }),
            memberRoles = TeamMemberRoles.All,
            roles = Roles.All,
        };
    }

    /* ------------------------------------------------------------------ *
     * helpers
     * ------------------------------------------------------------------ */

    private async Task<ActionResult<TeamDto>> OneTeam(int id, CancellationToken ct)
    {
        var rows = await Teams(includeRetired: true, includePast: false, ct);
        var row = rows.FirstOrDefault(r => r.Id == id);

        return row is null ? NotFound() : row;
    }

    /// <summary>
    /// Structural changes are an administrator's job.
    ///
    /// Checked here rather than by an attribute because these endpoints sit on
    /// the Administration module, which a MIS or HR user can also reach — they
    /// may read the org chart, and moving people between desks is not theirs.
    /// </summary>
    private void RequireAdmin()
    {
        if (tenant.Role is Roles.SuperAdmin or Roles.CompanyAdmin) return;

        throw ApiException.Forbidden(
            "Only an administrator can change the company profile, its titles, or its teams.");
    }

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Upper(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
