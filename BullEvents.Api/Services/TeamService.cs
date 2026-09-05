using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>
/// Teams, and who is on them.
///
/// The rule that shapes everything here: a membership is closed with a date,
/// never deleted. "Who was on the Andheri desk in September" is the question
/// every incentive dispute turns on, and a row that disappeared when somebody
/// moved cannot answer it — the commission was earned by whoever held the desk
/// at the time, not by whoever holds it now.
///
/// So "remove" ends a membership and "transfer" ends one and opens another, and
/// both leave the history intact.
/// </summary>
public class TeamService(AppDbContext db, TenantContext tenant)
{
    /* ------------------------------------------------------------------ *
     * Teams
     * ------------------------------------------------------------------ */

    public async Task<Team> SaveAsync(
        int? id,
        string name,
        string? code,
        string kind,
        int? branchId,
        int? leadUserId,
        int? parentTeamId,
        string? description,
        bool isActive,
        CancellationToken ct = default)
    {
        if (!TeamKinds.All.Contains(kind))
        {
            throw ApiException.BadRequest($"'{kind}' is not a kind of team this system knows.");
        }

        var team = id is int existing
            ? await db.Set<Team>().FirstOrDefaultAsync(t => t.Id == existing, ct)
                ?? throw ApiException.NotFound("Team")
            : new Team { CompanyId = tenant.CompanyId };

        // A team that is its own ancestor makes every roll-up recurse forever.
        // Checked to any depth rather than one level, because the two-step
        // version of this mistake is the one people actually make.
        if (parentTeamId is int parent)
        {
            if (team.Id != 0 && await IsDescendantAsync(parent, team.Id, ct))
            {
                throw ApiException.BadRequest(
                    "That would make the team sit under one of its own sub-teams.");
            }

            if (parent == team.Id)
            {
                throw ApiException.BadRequest("A team cannot be its own parent.");
            }
        }

        if (leadUserId is int lead && !await db.Users.AnyAsync(u => u.Id == lead && u.IsActive, ct))
        {
            throw ApiException.BadRequest("That team lead is not an active user.");
        }

        var duplicate = await db.Set<Team>()
            .AnyAsync(t => t.Id != team.Id
                && EF.Functions.Collate(t.Name, "C") == name
                && t.IsActive, ct);

        if (duplicate)
        {
            throw ApiException.BadRequest($"A team called '{name}' already exists.");
        }

        team.Name = name;
        team.Code = code;
        team.Kind = kind;
        team.BranchId = branchId;
        team.LeadUserId = leadUserId;
        team.ParentTeamId = parentTeamId;
        team.Description = description;
        team.IsActive = isActive;
        team.UpdatedAt = DateTime.UtcNow;

        if (team.Id == 0) db.Set<Team>().Add(team);

        await db.SaveChangesAsync(ct);

        // The lead belongs on their own team. Added silently rather than
        // demanded of the operator, because a team whose lead is not a member
        // reports a headcount one short and drops them out of every team filter.
        if (team.LeadUserId is int leader)
        {
            await EnsureMemberAsync(team.Id, leader, TeamMemberRoles.Lead, ct);
        }

        return team;
    }

    /// <summary>
    /// Retires a team.
    ///
    /// Never deleted, and refused while people are still on it — a team that
    /// vanished from under six live memberships leaves those people on a desk
    /// that no longer exists, which no report can render. Move them first, and
    /// the refusal says how many.
    /// </summary>
    public async Task<Team> RetireAsync(int id, CancellationToken ct = default)
    {
        var team = await db.Set<Team>().FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw ApiException.NotFound("Team");

        var live = await db.Set<TeamMember>()
            .CountAsync(m => m.TeamId == id && m.LeftOn == null, ct);

        if (live > 0)
        {
            throw ApiException.BadRequest(
                $"{team.Name} still has {live} member{(live == 1 ? "" : "s")}. "
                + "Move or remove them first.");
        }

        var children = await db.Set<Team>()
            .CountAsync(t => t.ParentTeamId == id && t.IsActive, ct);

        if (children > 0)
        {
            throw ApiException.BadRequest(
                $"{team.Name} still has {children} sub-team{(children == 1 ? "" : "s")} under it.");
        }

        team.IsActive = false;
        team.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return team;
    }

    /* ------------------------------------------------------------------ *
     * Membership
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Puts somebody on a team.
    ///
    /// A person may sit on several — a specialist shared across two desks is
    /// ordinary — but exactly one live membership is their primary, so a
    /// headcount by team does not count them twice.
    /// </summary>
    public async Task<TeamMember> AddMemberAsync(
        int teamId,
        int userId,
        string roleInTeam,
        DateTime? joinedOn,
        bool isPrimary,
        string? notes,
        CancellationToken ct = default)
    {
        var team = await db.Set<Team>().FirstOrDefaultAsync(t => t.Id == teamId, ct)
            ?? throw ApiException.NotFound("Team");

        if (!team.IsActive)
        {
            throw ApiException.BadRequest($"{team.Name} has been retired.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw ApiException.NotFound("User");

        if (!user.IsActive)
        {
            throw ApiException.BadRequest(
                $"{user.Name} is deactivated. Reactivate the account before putting them on a team.");
        }

        if (!TeamMemberRoles.All.Contains(roleInTeam))
        {
            throw ApiException.BadRequest($"'{roleInTeam}' is not a role on a team.");
        }

        var already = await db.Set<TeamMember>()
            .AnyAsync(m => m.TeamId == teamId && m.UserId == userId && m.LeftOn == null, ct);

        if (already)
        {
            throw ApiException.BadRequest($"{user.Name} is already on {team.Name}.");
        }

        var member = new TeamMember
        {
            CompanyId = tenant.CompanyId,
            TeamId = teamId,
            UserId = userId,
            RoleInTeam = roleInTeam,
            JoinedOn = (joinedOn ?? DateTime.UtcNow).Date,
            IsPrimary = isPrimary,
            Notes = notes,
        };

        db.Set<TeamMember>().Add(member);

        if (isPrimary) await DemoteOtherPrimariesAsync(userId, member, ct);

        // Their first team is a join, not a transfer, and the null origin says
        // so — which is what makes an arrivals report possible.
        db.Set<TeamTransfer>().Add(new TeamTransfer
        {
            CompanyId = tenant.CompanyId,
            UserId = userId,
            FromTeamId = null,
            ToTeamId = teamId,
            EffectiveOn = member.JoinedOn,
            Reason = notes ?? "Added to the team.",
            MovedByUserId = tenant.UserId,
            MovedByName = tenant.UserName,
        });

        await db.SaveChangesAsync(ct);
        return member;
    }

    /// <summary>
    /// Takes somebody off a team.
    ///
    /// The membership is closed, not deleted. If they were leading it the team
    /// is left without a lead rather than promoting whoever is next by title —
    /// picking a lead is a decision, and guessing it silently puts the wrong
    /// name on a desk.
    /// </summary>
    public async Task RemoveMemberAsync(
        int teamId,
        int userId,
        DateTime? leftOn,
        string? reason,
        CancellationToken ct = default)
    {
        var member = await db.Set<TeamMember>()
            .Include(m => m.Team)
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == userId && m.LeftOn == null, ct)
            ?? throw ApiException.BadRequest("That person is not on this team.");

        var day = (leftOn ?? DateTime.UtcNow).Date;

        if (day < member.JoinedOn)
        {
            throw ApiException.BadRequest(
                $"They joined on {member.JoinedOn:dd MMM yyyy} and cannot have left before that.");
        }

        member.LeftOn = day;
        member.IsPrimary = false;

        if (member.Team?.LeadUserId == userId)
        {
            member.Team.LeadUserId = null;
            member.Team.UpdatedAt = DateTime.UtcNow;
        }

        db.Set<TeamTransfer>().Add(new TeamTransfer
        {
            CompanyId = tenant.CompanyId,
            UserId = userId,
            FromTeamId = teamId,
            ToTeamId = null,
            EffectiveOn = day,
            Reason = reason ?? "Removed from the team.",
            MovedByUserId = tenant.UserId,
            MovedByName = tenant.UserName,
        });

        await db.SaveChangesAsync(ct);

        // Losing their primary leaves them with teams but no home one, which
        // breaks headcount. The oldest live membership takes over — the desk
        // they have been on longest is the least surprising answer.
        await RepairPrimaryAsync(userId, ct);
    }

    /// <summary>
    /// Moves somebody from one team to another in a single act.
    ///
    /// Not "remove then add" by the operator, because those are two clicks that
    /// can be half-done: a transfer interrupted between them leaves a person on
    /// no desk at all, and the arrivals and departures reports then disagree
    /// with each other for as long as nobody notices.
    /// </summary>
    public async Task<TeamMember> TransferAsync(
        int userId,
        int fromTeamId,
        int toTeamId,
        DateTime? effectiveOn,
        string? reason,
        string? roleInTeam,
        CancellationToken ct = default)
    {
        if (fromTeamId == toTeamId)
        {
            throw ApiException.BadRequest("They are already on that team.");
        }

        var from = await db.Set<Team>().FirstOrDefaultAsync(t => t.Id == fromTeamId, ct)
            ?? throw ApiException.NotFound("The team they are moving from");

        var to = await db.Set<Team>().FirstOrDefaultAsync(t => t.Id == toTeamId, ct)
            ?? throw ApiException.NotFound("The team they are moving to");

        if (!to.IsActive)
        {
            throw ApiException.BadRequest($"{to.Name} has been retired, so nobody can move onto it.");
        }

        var current = await db.Set<TeamMember>()
            .FirstOrDefaultAsync(m => m.TeamId == fromTeamId && m.UserId == userId && m.LeftOn == null, ct)
            ?? throw ApiException.BadRequest($"They are not on {from.Name}.");

        var alreadyThere = await db.Set<TeamMember>()
            .AnyAsync(m => m.TeamId == toTeamId && m.UserId == userId && m.LeftOn == null, ct);

        if (alreadyThere)
        {
            throw ApiException.BadRequest($"They are already on {to.Name}.");
        }

        var day = (effectiveOn ?? DateTime.UtcNow).Date;

        if (day < current.JoinedOn)
        {
            throw ApiException.BadRequest(
                $"They joined {from.Name} on {current.JoinedOn:dd MMM yyyy}; "
                + "the move cannot be dated before that.");
        }

        var wasPrimary = current.IsPrimary;

        // Both sides land in one SaveChanges, so an interrupted transfer leaves
        // the person where they were rather than nowhere.
        current.LeftOn = day;
        current.IsPrimary = false;

        if (from.LeadUserId == userId)
        {
            from.LeadUserId = null;
            from.UpdatedAt = DateTime.UtcNow;
        }

        var moved = new TeamMember
        {
            CompanyId = tenant.CompanyId,
            TeamId = toTeamId,
            UserId = userId,
            RoleInTeam = roleInTeam is not null && TeamMemberRoles.All.Contains(roleInTeam)
                ? roleInTeam
                // A lead who moves is not automatically a lead of the new team;
                // that is the receiving manager's call.
                : TeamMemberRoles.Member,
            JoinedOn = day,
            IsPrimary = wasPrimary,
            Notes = reason,
        };

        db.Set<TeamMember>().Add(moved);

        db.Set<TeamTransfer>().Add(new TeamTransfer
        {
            CompanyId = tenant.CompanyId,
            UserId = userId,
            FromTeamId = fromTeamId,
            ToTeamId = toTeamId,
            EffectiveOn = day,
            Reason = reason,
            MovedByUserId = tenant.UserId,
            MovedByName = tenant.UserName,
        });

        if (wasPrimary) await DemoteOtherPrimariesAsync(userId, moved, ct);

        await db.SaveChangesAsync(ct);
        return moved;
    }

    /* ------------------------------------------------------------------ *
     * helpers
     * ------------------------------------------------------------------ */

    /// <summary>Adds the member if they are not already on, used for a team's own lead.</summary>
    private async Task EnsureMemberAsync(
        int teamId, int userId, string roleInTeam, CancellationToken ct)
    {
        var member = await db.Set<TeamMember>()
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == userId && m.LeftOn == null, ct);

        if (member is not null)
        {
            if (member.RoleInTeam != roleInTeam)
            {
                member.RoleInTeam = roleInTeam;
                await db.SaveChangesAsync(ct);
            }

            return;
        }

        var anyPrimary = await db.Set<TeamMember>()
            .AnyAsync(m => m.UserId == userId && m.LeftOn == null && m.IsPrimary, ct);

        db.Set<TeamMember>().Add(new TeamMember
        {
            CompanyId = tenant.CompanyId,
            TeamId = teamId,
            UserId = userId,
            RoleInTeam = roleInTeam,
            JoinedOn = DateTime.UtcNow.Date,
            IsPrimary = !anyPrimary,
            Notes = "Added as the team lead.",
        });

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Leaves exactly one live membership marked primary.
    ///
    /// Takes the new row as an object rather than an id because it may not be
    /// saved yet — comparing on id would demote the very row being added.
    /// </summary>
    private async Task DemoteOtherPrimariesAsync(
        int userId, TeamMember keep, CancellationToken ct)
    {
        var others = await db.Set<TeamMember>()
            .Where(m => m.UserId == userId && m.LeftOn == null && m.IsPrimary)
            .ToListAsync(ct);

        foreach (var other in others)
        {
            if (!ReferenceEquals(other, keep)) other.IsPrimary = false;
        }
    }

    /// <summary>Gives a primary back to somebody left with teams but no home one.</summary>
    private async Task RepairPrimaryAsync(int userId, CancellationToken ct)
    {
        var live = await db.Set<TeamMember>()
            .Where(m => m.UserId == userId && m.LeftOn == null)
            .OrderBy(m => m.JoinedOn).ThenBy(m => m.Id)
            .ToListAsync(ct);

        if (live.Count == 0 || live.Any(m => m.IsPrimary)) return;

        live[0].IsPrimary = true;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Walks up from <paramref name="teamId"/> looking for <paramref name="ancestorId"/>.</summary>
    private async Task<bool> IsDescendantAsync(int teamId, int ancestorId, CancellationToken ct)
    {
        var seen = new HashSet<int>();
        int? cursor = teamId;

        // Guarded against a cycle already in the data, which a previous bad
        // write could have left — walking one would hang the request.
        while (cursor is int id && seen.Add(id))
        {
            if (id == ancestorId) return true;

            cursor = await db.Set<Team>()
                .Where(t => t.Id == id)
                .Select(t => t.ParentTeamId)
                .FirstOrDefaultAsync(ct);
        }

        return false;
    }
}
