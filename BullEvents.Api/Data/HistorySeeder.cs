using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Data;

/// <summary>
/// Generates eighteen months of closed lead history so the workspace behaves
/// like a live one: the list view is dense enough for filters and paging to
/// matter, dashboards have something to aggregate, and — the reason this exists
/// — the ML.NET conversion model has enough labelled outcomes to actually train.
///
/// Outcomes are not random. Each lead's conversion probability is built from the
/// same signals a real pipeline shows (source quality, priority, how quickly and
/// how often it was worked), then sampled with noise. That gives the model a
/// genuine pattern to learn instead of coin flips.
/// </summary>
public static class HistorySeeder
{
    private const int TargetHistoricalLeads = 420;

    private static readonly string[] FirstNames =
    [
        "Aarav", "Vivaan", "Aditya", "Vihaan", "Arjun", "Reyansh", "Krishna", "Ishaan",
        "Rohan", "Kabir", "Ananya", "Diya", "Aadhya", "Saanvi", "Meera", "Riya",
        "Neha", "Pooja", "Sneha", "Divya", "Rahul", "Amit", "Vikram", "Sanjay",
        "Karan", "Nikhil", "Priya", "Kavya", "Ishita", "Tanvi", "Manish", "Deepak",
        "Farhan", "Zoya", "Imran", "Ayesha", "Rajesh", "Suresh", "Anil", "Kiran"
    ];

    private static readonly string[] LastNames =
    [
        "Sharma", "Verma", "Patel", "Nair", "Iyer", "Reddy", "Mehta", "Joshi",
        "Kapoor", "Malhotra", "Desai", "Chopra", "Sethi", "Rao", "Gupta", "Singh",
        "Bhatt", "Kulkarni", "Menon", "Shah", "Agarwal", "Bose", "Chauhan", "Pillai"
    ];

    private static readonly string[] CompanySuffixes =
    [
        "Enterprises", "Textiles", "Exports", "Realty Advisors", "Consulting",
        "Logistics", "& Associates", "Industries", "Traders", "Ventures"
    ];

    private static readonly (string City, string Country)[] Cities =
    [
        ("Mumbai", "India"), ("Pune", "India"), ("Gurugram", "India"),
        ("Bengaluru", "India"), ("Hyderabad", "India"), ("Delhi", "India"),
        ("Thane", "India"), ("Navi Mumbai", "India")
    ];

    /// <summary>Base conversion likelihood by acquisition channel.</summary>
    private static readonly Dictionary<string, double> SourceStrength = new()
    {
        [LeadSources.Referral] = 0.34,
        [LeadSources.VendorPartner] = 0.28,
        [LeadSources.WalkIn] = 0.26,
        [LeadSources.Website] = 0.17,
        [LeadSources.EventPortal] = 0.13,
        [LeadSources.SocialAds] = 0.08,
        [LeadSources.Other] = 0.10
    };

    private static readonly Dictionary<string, double> PriorityStrength = new()
    {
        [LeadPriorities.Hot] = 0.22,
        [LeadPriorities.High] = 0.12,
        [LeadPriorities.Medium] = 0.0,
        [LeadPriorities.Low] = -0.10
    };

    public static async Task SeedAsync(AppDbContext db)
    {
        var closedCount = await db.Leads.CountAsync(l =>
            l.Stage == LeadStages.Booked ||
            l.Stage == LeadStages.Lost ||
            l.Stage == LeadStages.Booked);

        // Only backfill once — presence of real closed history means we're done.
        if (closedCount >= 50) return;

        var company = await db.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync();
        if (company is null) return;

        var branchIds = await db.Branches
            .Where(b => b.CompanyId == company.Id)
            .Select(b => b.Id)
            .ToListAsync();
        if (branchIds.Count == 0) return;

        var ownerIds = await db.Users
            .Where(u => u.CompanyId == company.Id)
            .Select(u => u.Id)
            .ToListAsync();

        var ownerNames = await db.Users
            .Where(u => u.CompanyId == company.Id)
            .ToDictionaryAsync(u => u.Id, u => u.Name);

        // Fixed seed keeps the demo workspace identical between rebuilds.
        var rng = new Random(20260822);
        var now = DateTime.UtcNow;

        var leads = new List<Lead>(TargetHistoricalLeads);
        var plans = new List<(Lead Lead, bool Converted, int Touches, DateTime FirstTouch)>();

        for (var i = 0; i < TargetHistoricalLeads; i++)
        {
            var source = Pick(rng, LeadSources.All);
            var priority = WeightedPriority(rng);
            var (city, country) = Cities[rng.Next(Cities.Length)];

            var first = FirstNames[rng.Next(FirstNames.Length)];
            var last = LastNames[rng.Next(LastNames.Length)];
            var name = $"{first} {last}";

            var hasCompany = rng.NextDouble() < 0.45;
            var hasEmail = rng.NextDouble() < 0.82;

            // Spread creation across the last 18 months, ending a month ago so
            // every generated lead has had time to reach an outcome.
            var createdAt = now.AddDays(-rng.Next(30, 545)).AddHours(-rng.Next(0, 24));

            // How fast the first follow-up happened, and how hard it was worked.
            var responseHours = Math.Round(Math.Pow(rng.NextDouble(), 2) * 96, 1);
            var touches = 1 + rng.Next(0, 9);

            var probability =
                SourceStrength[source]
                + PriorityStrength[priority]
                + (responseHours <= 4 ? 0.16 : responseHours <= 24 ? 0.06 : -0.08)
                + Math.Min(0.18, touches * 0.025)
                + (hasEmail ? 0.04 : -0.06)
                + (hasCompany ? 0.03 : 0.0)
                + (rng.NextDouble() - 0.5) * 0.16; // noise

            var converted = rng.NextDouble() < Math.Clamp(probability, 0.02, 0.92);

            var lead = new Lead
            {
                CompanyId = company.Id,
                BranchId = branchIds[rng.Next(branchIds.Count)],
                Salutation = rng.NextDouble() < 0.5 ? "Mr." : "Ms.",
                Name = name,
                CompanyName = hasCompany
                    ? $"{last} {CompanySuffixes[rng.Next(CompanySuffixes.Length)]}"
                    : null,
                Phone = $"9{rng.Next(100000000, 999999999)}",
                Email = hasEmail
                    ? $"{first.ToLowerInvariant()}.{last.ToLowerInvariant()}{i}@example.com"
                    : null,
                City = city,
                Country = country,
                Source = source,
                Priority = priority,
                Stage = converted ? LeadStages.Booked : LeadStages.Lost,
                OwnerId = ownerIds.Count > 0 ? ownerIds[rng.Next(ownerIds.Count)] : null,
                Notes = converted
                    ? "Closed — booking completed."
                    : "Closed — did not proceed.",
                CreatedAt = createdAt,
                UpdatedAt = createdAt.AddDays(rng.Next(3, 60))
            };

            leads.Add(lead);
            plans.Add((lead, converted, touches, createdAt.AddHours(responseHours)));
        }

        db.Leads.AddRange(leads);
        await db.SaveChangesAsync();

        // Build a plausible activity trail so engagement features are real.
        var activities = new List<LeadActivity>(TargetHistoricalLeads * 5);

        foreach (var (lead, converted, touches, firstTouch) in plans)
        {
            var actorId = lead.OwnerId ?? 0;
            var actorName = ownerNames.TryGetValue(actorId, out var n) ? n : "System";

            activities.Add(new LeadActivity
            {
                LeadId = lead.Id,
                Type = LeadActivityTypes.Created,
                Remarks = $"Lead captured from {lead.Source}.",
                ActorId = actorId,
                ActorName = actorName,
                CreatedAt = lead.CreatedAt
            });

            var cursor = firstTouch;
            for (var t = 0; t < touches; t++)
            {
                activities.Add(new LeadActivity
                {
                    LeadId = lead.Id,
                    // Parenthesised: `t % 3 switch` would bind as `t % (3 switch …)`.
                    Type = (t % 3) switch
                    {
                        0 => LeadActivityTypes.Call,
                        1 => LeadActivityTypes.WhatsApp,
                        _ => LeadActivityTypes.SiteVisit
                    },
                    Remarks = null,
                    ActorId = actorId,
                    ActorName = actorName,
                    CreatedAt = cursor
                });
                cursor = cursor.AddDays(rng.Next(1, 12));
            }

            activities.Add(new LeadActivity
            {
                LeadId = lead.Id,
                Type = LeadActivityTypes.StageChange,
                FromStage = LeadStages.Negotiation,
                ToStage = converted ? LeadStages.Booked : LeadStages.Lost,
                ActorId = actorId,
                ActorName = actorName,
                CreatedAt = lead.UpdatedAt
            });
        }

        db.LeadActivities.AddRange(activities);
        await db.SaveChangesAsync();
    }

    private static string Pick(Random rng, string[] options) => options[rng.Next(options.Length)];

    private static string WeightedPriority(Random rng)
    {
        var roll = rng.NextDouble();
        return roll switch
        {
            < 0.12 => LeadPriorities.Hot,
            < 0.34 => LeadPriorities.High,
            < 0.78 => LeadPriorities.Medium,
            _ => LeadPriorities.Low
        };
    }
}
