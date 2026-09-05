using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>
/// Everything one request is allowed to do, resolved once and then answered
/// from memory.
/// </summary>
public record EffectivePermissions(
    RoleDefinition Role,
    IReadOnlyDictionary<string, ObjectAction> Objects,
    IReadOnlyDictionary<string, FieldRule> Fields,
    IReadOnlySet<string> Modules,
    IReadOnlyCollection<int>? VisibleOwnerIds,
    IReadOnlyDictionary<string, ObjectVisibility> Visibility)
{
    /// <summary>What this request may do to an object, after every grant is folded in.</summary>
    public ObjectAction On(string securedObject) =>
        Objects.TryGetValue(securedObject, out var actions) ? actions : ObjectAction.None;

    public bool Can(string securedObject, ObjectAction action) =>
        (On(securedObject) & action) == action;

    /// <summary>
    /// True when the caller reads every record of an object regardless of who
    /// owns it — either through an explicit ViewAll or a company-wide scope.
    /// </summary>
    public bool SeesAllRecordsOf(string securedObject) =>
        VisibleOwnerIds is null || Can(securedObject, ObjectAction.ViewAll);

    public bool HasModule(string module) => Modules.Contains(module);

    /// <summary>Whether a field may be read. Unlisted fields follow their object.</summary>
    public bool CanReadField(string securedObject, string field) =>
        !Fields.TryGetValue(Key(securedObject, field), out var rule) || rule.CanRead;

    public bool CanEditField(string securedObject, string field) =>
        !Fields.TryGetValue(Key(securedObject, field), out var rule) || rule.CanEdit;

    internal static string Key(string securedObject, string field) =>
        $"{securedObject}.{field}";
}

public record FieldRule(bool CanRead, bool CanEdit);

/// <summary>
/// Folds a user's role, profile, permission sets and their company's sharing
/// configuration into one answer.
///
/// The order matters and is fixed: the role sets the shape, the profile is the
/// baseline, permission sets only add, and the company's object visibility
/// decides how far sharing reaches. Nothing in the chain subtracts — the only
/// way to hold less is to be granted less, which is what makes an effective
/// permission explainable to the person who has it.
///
/// Resolved once per request. Every screen asks this several times, and the
/// underlying reads are four small queries against tables with a few dozen rows.
/// </summary>
public class PermissionResolver(AppDbContext db, TenantContext tenant, AccessScope scope)
{
    private EffectivePermissions? _cached;

    public async Task<EffectivePermissions> ResolveAsync(CancellationToken ct = default)
    {
        if (_cached is not null) return _cached;

        var role = RoleCatalog.For(tenant.Role);

        /* ---------------- the bundles this user holds ---------------- */

        var now = DateTime.UtcNow;

        // The profile for their role plus every unexpired permission set. Both
        // come from one query because they live in one table.
        var sets = await db.PermissionSets
            .Include(p => p.ObjectPermissions)
            .Include(p => p.FieldPermissions)
            .Where(p =>
                (p.IsProfile && p.RoleKey == tenant.Role)
                || p.Assignments.Any(a =>
                    a.UserId == tenant.UserId
                    && (a.ExpiresAt == null || a.ExpiresAt > now)))
            .AsNoTracking()
            .ToListAsync(ct);

        /* ---------------- objects ---------------- */

        var objects = new Dictionary<string, ObjectAction>(StringComparer.OrdinalIgnoreCase);

        // A platform operator is not a permission question. Resolving them
        // through the same table would mean a misconfigured row could lock the
        // person who fixes misconfigured rows out of the console.
        if (role.Scope == DataScope.Platform)
        {
            foreach (var name in SecuredObjects.All) objects[name] = ObjectAction.Everything;
        }
        else
        {
            foreach (var set in sets)
            {
                foreach (var grant in set.ObjectPermissions)
                {
                    objects[grant.Object] = objects.TryGetValue(grant.Object, out var existing)
                        ? existing | grant.Actions
                        : grant.Actions;
                }
            }

            // Nothing configured yet: fall back to what the role means rather
            // than to nothing at all, so a company that has never opened the
            // console still has a working CRM.
            if (objects.Count == 0) ApplyRoleDefaults(role, objects);

            // A reporting seat reads widely and writes nothing, whatever any
            // bundle says. Enforced here rather than trusted to the data,
            // because "read-only" is the whole point of the seat.
            if (role.ReadOnly)
            {
                foreach (var key in objects.Keys.ToList())
                {
                    objects[key] &= ObjectAction.View | ObjectAction.ViewAll;
                }
            }
        }

        /* ---------------- fields ---------------- */

        var fields = new Dictionary<string, FieldRule>(StringComparer.OrdinalIgnoreCase);

        foreach (var set in sets)
        {
            foreach (var rule in set.FieldPermissions)
            {
                var key = EffectivePermissions.Key(rule.Object, rule.Field);

                // Additive here too: holding two bundles gives the more
                // generous of the two, never the more restrictive.
                fields[key] = fields.TryGetValue(key, out var existing)
                    ? new FieldRule(existing.CanRead || rule.CanRead,
                                    existing.CanEdit || rule.CanEdit)
                    : new FieldRule(rule.CanRead, rule.CanEdit);
            }
        }

        /* ---------------- modules ---------------- */

        var modules = new HashSet<string>(role.Modules, StringComparer.OrdinalIgnoreCase);

        foreach (var set in sets)
        {
            if (string.IsNullOrWhiteSpace(set.ModulesCsv)) continue;

            foreach (var module in set.ModulesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                modules.Add(module.Trim());
            }
        }

        // The per-user overrides are the last word, and the only place in the
        // chain that may take something away — because that is exactly what an
        // administrator means when they untick a module for one person.
        var overrides = await db.UserModuleGrants
            .Where(g => g.UserId == tenant.UserId)
            .AsNoTracking()
            .ToListAsync(ct);

        foreach (var grant in overrides)
        {
            if (grant.Granted) modules.Add(grant.Module);
            else modules.Remove(grant.Module);
        }

        /* ---------------- sharing ---------------- */

        var visibility = await db.ObjectVisibilityRules
            .AsNoTracking()
            .ToDictionaryAsync(v => v.Object, v => v.Visibility, StringComparer.OrdinalIgnoreCase, ct);

        _cached = new EffectivePermissions(
            role,
            objects,
            fields,
            modules,
            await scope.VisibleOwnerIdsAsync(ct),
            visibility);

        return _cached;
    }

    /// <summary>
    /// What a role means before anybody has configured anything.
    ///
    /// This is the shipped default that makes a fresh company usable; the
    /// console writes real rows the moment somebody edits a profile, and these
    /// stop being consulted.
    /// </summary>
    private static void ApplyRoleDefaults(
        RoleDefinition role, Dictionary<string, ObjectAction> objects)
    {
        if (role.PeopleData)
        {
            objects[SecuredObjects.Employee] = ObjectAction.Full | ObjectAction.ViewAll;
            objects[SecuredObjects.User] = ObjectAction.Read | ObjectAction.ViewAll;
            objects[SecuredObjects.Branch] = ObjectAction.Read;
            objects[SecuredObjects.Company] = ObjectAction.Read;
            objects[SecuredObjects.Goal] = ObjectAction.Read;
            objects[SecuredObjects.Report] = ObjectAction.Read;
            return;
        }

        var sales = role.ReadOnly ? ObjectAction.View : ObjectAction.Full;

        foreach (var name in SecuredObjects.SalesObjects) objects[name] = sales;

        // The three lists every seat needs to read whatever else it does: you
        // cannot assign a lead without seeing the people, or file one against a
        // branch you cannot name. Read-only by default — creating a branch or a
        // colleague is an administrator's job, and the grants below say so.
        objects[SecuredObjects.User] = ObjectAction.View;
        objects[SecuredObjects.Branch] = ObjectAction.View;
        objects[SecuredObjects.Company] = ObjectAction.View;

        objects[SecuredObjects.Project] = ObjectAction.View;
        objects[SecuredObjects.Report] = ObjectAction.View;

        // Everybody sees the targets they are measured against. Setting them is
        // a manager's job, so the write verbs follow the scope rather than the
        // seat: an own-records role reads its number and cannot move it.
        objects[SecuredObjects.Goal] = role.Scope >= DataScope.Team
            ? ObjectAction.Full
            : ObjectAction.View;

        if (role.WonBusinessOnly)
        {
            objects[SecuredObjects.Booking] = ObjectAction.Full | ObjectAction.ViewAll;
        }
        else if (role.Scope >= DataScope.Team)
        {
            // A manager can see what their team sold without being able to touch
            // the money. Reps get nothing here — post-sales is a different desk.
            objects[SecuredObjects.Booking] = ObjectAction.View;
        }

        // Company scope means seeing the whole company, which is what ViewAll
        // says in permission terms — without it the row filter would still cut
        // a Back Office seat down to its own records.
        if (role.Scope is DataScope.Company or DataScope.Platform)
        {
            foreach (var key in objects.Keys.ToList()) objects[key] |= ObjectAction.ViewAll;
        }

        if (role.Key == Roles.CompanyAdmin)
        {
            // "Runs one company. Sees everything inside it" — so everything
            // inside it, including the objects this role's own day never
            // touches. Building the set from the objects already in the
            // dictionary left a company admin with no permission at all on
            // anything the sales defaults had not mentioned: bookings were
            // invisible to the person who administers the company that takes
            // them.
            foreach (var name in SecuredObjects.All)
            {
                objects[name] = ObjectAction.Everything;
            }

            // The one exception. A company admin edits their own company row;
            // creating and deleting tenants is a platform-operator action.
            objects[SecuredObjects.Company] =
                ObjectAction.Read | ObjectAction.Edit | ObjectAction.ViewAll | ObjectAction.ModifyAll;
        }
    }
}
