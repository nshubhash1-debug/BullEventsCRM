using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Data;

/// <summary>
/// Writes the shipped profiles and object visibility for a company.
///
/// The resolver falls back to the role's defaults when nothing is configured,
/// so this is not what makes the CRM work — it is what makes the console have
/// something to show and edit. A company that never opens the console behaves
/// identically either way; one that does gets rows it can reason about instead
/// of an empty screen and an invisible policy.
///
/// Idempotent, and it never overwrites. Once an administrator has touched a
/// profile it is theirs, and a redeploy that quietly reset everyone's
/// permissions back to the shipped set would be a security incident.
/// </summary>
public static class PermissionSeeder
{
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        var companies = await db.Companies.Select(c => c.Id).ToListAsync(ct);
        if (companies.Count == 0) return;

        foreach (var companyId in companies)
        {
            await SeedProfilesAsync(db, companyId, ct);
            await SeedVisibilityAsync(db, companyId, ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedProfilesAsync(AppDbContext db, int companyId, CancellationToken ct)
    {
        var existing = await db.PermissionSets
            .IgnoreQueryFilters()
            .Where(p => p.CompanyId == companyId && p.IsProfile && p.RoleKey != null)
            .Select(p => p.RoleKey!)
            .ToListAsync(ct);

        var have = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var role in RoleCatalog.All)
        {
            // The platform operator is resolved outside the permission tables
            // entirely, so a profile for it would be a row that never gets read.
            if (role.Scope == DataScope.Platform) continue;
            if (have.Contains(role.Key)) continue;

            var profile = new PermissionSet
            {
                CompanyId = companyId,
                Name = role.Name,
                Description = role.Description,
                IsProfile = true,
                IsSystem = true,
                RoleKey = role.Key,
                ModulesCsv = string.Join(',', role.Modules),
            };

            foreach (var (name, actions) in DefaultsFor(role))
            {
                profile.ObjectPermissions.Add(new ObjectPermission
                {
                    Object = name,
                    Actions = actions,
                });
            }

            db.PermissionSets.Add(profile);
        }

        await TopUpBaselineAsync(db, companyId, ct);
    }

    /// <summary>
    /// The read grants every seat needs, added to profiles that already exist.
    ///
    /// This is the one place the seeder touches a profile it did not create, and
    /// it is deliberately one-directional: it may add a View bit, never clear
    /// one. The reason it has to exist at all is that these objects became gated
    /// after the first profiles were written — before, anybody authenticated
    /// could read the user and branch lists because nothing checked, and a
    /// profile from that era carries no grant saying so. Without this top-up,
    /// switching the checks on would take away access nobody ever decided to
    /// remove.
    /// </summary>
    private static async Task TopUpBaselineAsync(AppDbContext db, int companyId, CancellationToken ct)
    {
        string[] baseline =
        [
            SecuredObjects.User,
            SecuredObjects.Branch,
            SecuredObjects.Company,
            SecuredObjects.Goal,
            SecuredObjects.Report,
        ];

        var profiles = await db.PermissionSets
            .IgnoreQueryFilters()
            .Include(p => p.ObjectPermissions)
            .Where(p => p.CompanyId == companyId && p.IsProfile && p.RoleKey != null)
            .ToListAsync(ct);

        foreach (var profile in profiles)
        {
            // The company admin's profile is brought up to the whole catalogue.
            // Additive, like everything else in this method: it can only widen a
            // profile that was written before an object existed.
            if (string.Equals(profile.RoleKey, Roles.CompanyAdmin, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var name in SecuredObjects.All)
                {
                    var grant = profile.ObjectPermissions.FirstOrDefault(
                        o => string.Equals(o.Object, name, StringComparison.OrdinalIgnoreCase));

                    if (grant is null)
                    {
                        profile.ObjectPermissions.Add(new ObjectPermission
                        {
                            Object = name,
                            Actions = name == SecuredObjects.Company
                                ? ObjectAction.Read | ObjectAction.Edit
                                  | ObjectAction.ViewAll | ObjectAction.ModifyAll
                                : ObjectAction.Everything,
                        });
                    }
                    else if (name != SecuredObjects.Company)
                    {
                        grant.Actions |= ObjectAction.Everything;
                    }
                }

                continue;
            }

            foreach (var name in baseline)
            {
                var grant = profile.ObjectPermissions
                    .FirstOrDefault(o => string.Equals(o.Object, name, StringComparison.OrdinalIgnoreCase));

                if (grant is null)
                {
                    profile.ObjectPermissions.Add(new ObjectPermission
                    {
                        Object = name,
                        Actions = ObjectAction.View,
                    });
                }
                else
                {
                    grant.Actions |= ObjectAction.View;
                }
            }
        }
    }

    /// <summary>
    /// The object grants a role starts with — the same shape the resolver falls
    /// back to, written down so the console can show and change it.
    /// </summary>
    private static Dictionary<string, ObjectAction> DefaultsFor(RoleDefinition role)
    {
        var objects = new Dictionary<string, ObjectAction>(StringComparer.OrdinalIgnoreCase);

        if (role.PeopleData)
        {
            objects[SecuredObjects.Employee] = ObjectAction.Full | ObjectAction.ViewAll;
            objects[SecuredObjects.User] = ObjectAction.Read | ObjectAction.ViewAll;
            objects[SecuredObjects.Branch] = ObjectAction.Read;
            objects[SecuredObjects.Company] = ObjectAction.Read;
            objects[SecuredObjects.Goal] = ObjectAction.Read;
            objects[SecuredObjects.Report] = ObjectAction.Read;
            return objects;
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

        objects[SecuredObjects.Goal] = role.Scope >= DataScope.Team
            ? ObjectAction.Full
            : ObjectAction.View;

        if (role.WonBusinessOnly)
        {
            objects[SecuredObjects.Booking] = ObjectAction.Full | ObjectAction.ViewAll;
        }
        else if (role.Scope >= DataScope.Team)
        {
            objects[SecuredObjects.Booking] = ObjectAction.View;
        }

        if (role.Scope is DataScope.Company)
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

        return objects;
    }

    /// <summary>
    /// How far each object is shared before any rule widens it.
    ///
    /// The sales objects start Private, which is what makes a rep's pipeline
    /// theirs and lets the hierarchy do the rolling up. Reference data that
    /// everybody quotes from is readable by all — a unit nobody can see is a
    /// unit nobody can sell.
    /// </summary>
    private static async Task SeedVisibilityAsync(AppDbContext db, int companyId, CancellationToken ct)
    {
        var configured = await db.ObjectVisibilityRules
            .IgnoreQueryFilters()
            .Where(v => v.CompanyId == companyId)
            .Select(v => v.Object)
            .ToListAsync(ct);

        var have = configured.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var publicRead = new[]
        {
            SecuredObjects.Project, SecuredObjects.Unit,
            SecuredObjects.Branch, SecuredObjects.Company,
        };

        foreach (var name in SecuredObjects.All)
        {
            if (have.Contains(name)) continue;

            db.ObjectVisibilityRules.Add(new ObjectVisibilityRule
            {
                CompanyId = companyId,
                Object = name,
                Visibility = publicRead.Contains(name)
                    ? ObjectVisibility.PublicRead
                    : ObjectVisibility.Private,
                GrantAccessUsingHierarchy = true,
            });
        }
    }
}
