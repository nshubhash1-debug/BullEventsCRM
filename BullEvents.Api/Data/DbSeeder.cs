using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Data;

/// <summary>
/// What a working install needs before anyone can sign in: the home company,
/// its head office, its administrator, and the platform administrator.
///
/// The demonstration material — extra tenants for the company picker, sample
/// leads — is separated behind <paramref name="includeDemo"/> rather than mixed
/// in, because a company running its own data wants the install and not the
/// showroom. Deleting demo rows without switching this off would only mean
/// seeing them again on the next start.
/// </summary>
public static class DbSeeder
{
    /// <summary>
    /// The password the seeded accounts are created with.
    ///
    /// Read from configuration, never written here. A password in source is a
    /// password in every copy of the source — in the repository, in anyone's
    /// clone, and in the history after somebody deletes the line. When nothing
    /// is configured a different random one is generated per install and
    /// printed once at startup, so a fresh install is usable without ever
    /// having a password everybody already knows.
    ///
    /// Set it with <c>Seed:AdminPassword</c>, or the environment variable
    /// <c>Seed__AdminPassword</c>.
    /// </summary>
    private static string? _seedPassword;

    public static async Task SeedAsync(AppDbContext db, bool includeDemo) =>
        await SeedAsync(db, includeDemo, configuredPassword: null, announce: null);

    public static async Task SeedAsync(
        AppDbContext db,
        bool includeDemo,
        string? configuredPassword,
        Action<string>? announce)
    {
        if (_seedPassword is null)
        {
            var generated = string.IsNullOrWhiteSpace(configuredPassword);
            _seedPassword = generated ? GeneratePassword() : configuredPassword!;

            // Announced only when it was generated: printing a password
            // somebody deliberately configured would put it in the logs.
            if (generated && announce is not null)
            {
                announce($"No Seed:AdminPassword configured. Seeded accounts use "
                    + $"'{_seedPassword}' on this install. Set Seed:AdminPassword "
                    + "to choose your own, and change it after first sign-in.");
            }
        }

        await SeedFoundationAsync(db, includeDemo);
        await SeedPlatformAsync(db, includeDemo);
    }

    /// <summary>
    /// A password nobody has to remember and nobody else can guess.
    ///
    /// Long enough that it is not worth attacking and awkward enough that
    /// somebody changes it, which is the point.
    /// </summary>
    private static string GeneratePassword()
    {
        const string alphabet =
            "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(20);
        return string.Concat(bytes.Select(b => alphabet[b % alphabet.Length]));
    }

    /// <summary>The hash every seeded account is created with.</summary>
    private static string SeedPasswordHash() =>
        BCrypt.Net.BCrypt.HashPassword(
            _seedPassword ?? throw new InvalidOperationException(
                "SeedAsync must run before a seeded password is needed."));

    /// <summary>
    /// The platform tier: one super admin that belongs to no tenant in practice,
    /// plus a second and third company so the multi-company picker has something
    /// to pick between on a fresh install.
    ///
    /// Written as its own idempotent pass rather than inside the first-run block
    /// so it also lands on databases seeded before multi-company existed.
    /// </summary>
    private static async Task SeedPlatformAsync(AppDbContext db, bool includeDemo)
    {
        var homeCompany = await db.Companies.OrderBy(c => c.Id).FirstAsync();

        // The super admin below is part of the install and is seeded either way;
        // the two extra tenants exist only to give the company picker something
        // to pick between on a demonstration database.
        foreach (var (name, slug, city, planTier, adminName, adminEmail) in includeDemo ? new[]
        {
            ("Bull Events North", "bull-events-north", "Delhi", "Professional",
                "Rohan Verma", "rohan.verma@bulleventsnorth.com"),
            ("Saptapadi Weddings", "saptapadi-weddings", "Bengaluru", "Growth",
                "Anita Desai", "anita.desai@saptapadiweddings.com"),
        } : [])
        {
            if (await db.Companies.AnyAsync(c => c.Slug == slug)) continue;

            var company = new Company
            {
                Name = name,
                Slug = slug,
                PlanTier = planTier,
                Status = "Active",
            };
            db.Companies.Add(company);
            await db.SaveChangesAsync();

            var branch = new Branch { CompanyId = company.Id, Name = $"{city} HQ", City = city };
            db.Branches.Add(branch);

            var companyAdmin = new User
            {
                CompanyId = company.Id,
                Name = adminName,
                Email = adminEmail,
                PasswordHash = SeedPasswordHash(),
                Role = Roles.CompanyAdmin,
            };
            db.Users.Add(companyAdmin);
            await db.SaveChangesAsync();

            db.UserBranches.Add(new UserBranch { UserId = companyAdmin.Id, BranchId = branch.Id });
            await db.SaveChangesAsync();
        }

        if (!await db.Users.AnyAsync(u => u.Role == Roles.SuperAdmin))
        {
            db.Users.Add(new User
            {
                // The home company is only an anchor for the foreign key — a super
                // admin picks its working tenant at sign-in and the JWT is
                // re-issued against that choice.
                CompanyId = homeCompany.Id,
                Name = "Platform Administrator",
                Email = "superadmin@bulleventsglobal.com",
                PasswordHash = SeedPasswordHash(),
                Role = Roles.SuperAdmin,
            });
            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedFoundationAsync(AppDbContext db, bool includeDemo)
    {
        if (!await db.Companies.AnyAsync())
        {
            var company = new Company
            {
                Name = "Bull Events",
                Slug = "bull-events",
                PlanTier = "Enterprise",
                Status = "Active",
            };
            db.Companies.Add(company);
            await db.SaveChangesAsync();

            var branch = new Branch
            {
                CompanyId = company.Id,
                Name = "Mumbai HQ",
                City = "Mumbai",
            };
            db.Branches.Add(branch);
            await db.SaveChangesAsync();

            var admin = new User
            {
                CompanyId = company.Id,
                Name = "Priya Sharma",
                Email = "priya.sharma@bulleventsglobal.com",
                PasswordHash = SeedPasswordHash(),
                Role = Roles.CompanyAdmin,
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync();

            db.UserBranches.Add(new UserBranch { UserId = admin.Id, BranchId = branch.Id });
            await db.SaveChangesAsync();
        }

        if (includeDemo && !await db.Leads.AnyAsync())
        {
            var company = await db.Companies.OrderBy(c => c.Id).FirstAsync();
            var branch = await db.Branches.FirstAsync(b => b.CompanyId == company.Id);
            var owner = await db.Users.FirstOrDefaultAsync(u => u.CompanyId == company.Id);

            var demoLeads = new[]
            {
                new Lead { CompanyId = company.Id, BranchId = branch.Id, Salutation = "Mr.", Name = "Vikram Malhotra", CompanyName = "Malhotra Textiles", Phone = "9820011223", Email = "vikram.m@example.com", City = "Mumbai", Country = "India", Source = LeadSources.Website, Stage = LeadStages.New, Priority = LeadPriorities.High, OwnerId = owner?.Id, Notes = "Daughter’s wedding, budget 20-25 lakh." },
                new Lead { CompanyId = company.Id, BranchId = branch.Id, Salutation = "Ms.", Name = "Sneha Kapoor", Phone = "9820011224", Email = "sneha.k@example.com", City = "Mumbai", Country = "India", Source = LeadSources.EventPortal, Stage = LeadStages.New, Priority = LeadPriorities.Medium, OwnerId = owner?.Id },
                new Lead { CompanyId = company.Id, BranchId = branch.Id, Salutation = "Mr.", Name = "Arjun Nair", CompanyName = "Nair & Associates", Phone = "9820011225", Email = "arjun.n@example.com", City = "Mumbai", Country = "India", Source = LeadSources.Referral, Stage = LeadStages.Contacted, Priority = LeadPriorities.Medium, OwnerId = owner?.Id, Notes = "Referred by existing client." },
                new Lead { CompanyId = company.Id, BranchId = branch.Id, Salutation = "Ms.", Name = "Divya Rao", Phone = "9820011226", Email = "divya.r@example.com", City = "Mumbai", Country = "India", Source = LeadSources.SocialAds, Stage = LeadStages.SiteVisit, Priority = LeadPriorities.Hot, OwnerId = owner?.Id, Notes = "Venue visit scheduled for The Grand Palladium." },
                new Lead { CompanyId = company.Id, BranchId = branch.Id, Salutation = "Mr.", Name = "Kabir Sethi", CompanyName = "Sethi Exports", Phone = "9820011227", Email = "kabir.s@example.com", City = "Mumbai", Country = "India", Source = LeadSources.VendorPartner, Stage = LeadStages.Negotiation, Priority = LeadPriorities.Hot, OwnerId = owner?.Id, Notes = "Negotiating per-plate rate, close to decision." },
                new Lead { CompanyId = company.Id, BranchId = branch.Id, Salutation = "Ms.", Name = "Meera Joshi", Phone = "9820011228", Email = "meera.j@example.com", City = "Mumbai", Country = "India", Source = LeadSources.WalkIn, Stage = LeadStages.Booked, Priority = LeadPriorities.Medium, OwnerId = owner?.Id, Notes = "Booking confirmed, contract in progress." },
            };

            db.Leads.AddRange(demoLeads);
            await db.SaveChangesAsync();

            var actorName = owner?.Name ?? "System";
            var actorId = owner?.Id ?? 0;
            var now = DateTime.UtcNow;

            foreach (var lead in demoLeads)
            {
                db.LeadActivities.Add(new LeadActivity
                {
                    LeadId = lead.Id,
                    Type = LeadActivityTypes.Created,
                    Remarks = $"Lead captured from {lead.Source}.",
                    ActorId = actorId,
                    ActorName = actorName,
                    CreatedAt = now.AddDays(-6),
                });

                if (lead.Stage != LeadStages.New)
                {
                    db.LeadActivities.Add(new LeadActivity
                    {
                        LeadId = lead.Id,
                        Type = LeadActivityTypes.Call,
                        Remarks = "Initial call - discussed requirements.",
                        ActorId = actorId,
                        ActorName = actorName,
                        CreatedAt = now.AddDays(-4),
                    });
                    db.LeadActivities.Add(new LeadActivity
                    {
                        LeadId = lead.Id,
                        Type = LeadActivityTypes.StageChange,
                        FromStage = LeadStages.New,
                        ToStage = lead.Stage,
                        ActorId = actorId,
                        ActorName = actorName,
                        CreatedAt = now.AddDays(-3),
                    });
                }
            }

            await db.SaveChangesAsync();
        }
    }
}
