using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Data;

/// <summary>
/// A small, hand-written demonstration set: two venues with their spaces, and a
/// dozen enquiries spread across the pipeline.
///
/// Deliberately not <see cref="CrmSeeder"/>, which generates 380 leads, 220
/// contacts and 260 deals to make the list views look dense. That volume is
/// useful for testing a grid and useless for looking at a CRM — nobody can tell
/// whether a screen works when every row is noise. This is small enough to read
/// end to end, and every record is one a planner would recognise.
///
/// Additive and idempotent: it does nothing at all once a venue exists, so it
/// can never grow a second copy of itself or land on a tenant already carrying
/// real records.
/// </summary>
public static class EventDemoSeeder
{
    private record SpaceSeed(
        string Name,
        string Type,
        decimal Area,
        int Seating,
        int Floating,
        bool Outdoor,
        decimal Rental,
        decimal PerPlate,
        int MinPlates);

    private record VenueSeed(
        string Name,
        string Code,
        string OperatedBy,
        string Type,
        string City,
        string Locality,
        SpaceSeed[] Spaces);

    /// <summary>
    /// Seating sits well under floating throughout: round tables and a dance
    /// floor cost roughly half the standing count, and a venue quoting the
    /// floating number is how a wedding ends up with guests in the corridor.
    /// </summary>
    private static readonly VenueSeed[] Venues =
    [
        new("The Grand Palladium", "GPL", "Palladium Hospitality",
            VenueTypes.BanquetHall, "Mumbai", "Lower Parel",
            [
                new("Emerald Hall", "BanquetHall", 3200m, 250, 450, false, 250_000m, 1800m, 200),
                new("Ruby Ballroom", "Ballroom", 6500m, 550, 900, false, 500_000m, 2200m, 400),
                new("Pearl Pre-function", "PreFunctionArea", 900m, 60, 150, false, 75_000m, 1400m, 50),
            ]),

        new("Marina Bay Resort", "MBR", "Coastal Hospitality",
            VenueTypes.Resort, "Mumbai", "Worli",
            [
                new("Sunset Lawn", "Lawn", 12000m, 700, 1200, true, 650_000m, 2000m, 500),
                new("Poolside Deck", "Poolside", 4500m, 250, 450, true, 300_000m, 2400m, 200),
                new("Mandap Court", "MandapArea", 5000m, 300, 500, true, 350_000m, 1900m, 250),
            ]),
    ];

    private record LeadSeed(
        string Salutation,
        string Name,
        string? Partner,
        string Phone,
        string EventType,
        int DaysOut,
        int? RunDays,
        int Guests,
        string Slot,
        string? Functions,
        string Services,
        string Meal,
        decimal BudgetMin,
        decimal BudgetMax,
        string Source,
        string Stage,
        string Priority,
        string Locality,
        string? Notes);

    private static readonly LeadSeed[] Leads =
    [
        new("Mr.", "Vikram Malhotra", "Anjali Sharma", "9820011223", EventTypes.Wedding,
            172, 3, 450, EventSlots.Evening, "Mehendi,Sangeet,Wedding",
            "Venue,Catering,Decor,Photography", MealPreferences.Vegetarian,
            1_800_000m, 2_500_000m, LeadSources.EventPortal, LeadStages.New, LeadPriorities.Hot,
            "South Mumbai", "Wants a lawn for the sangeet, hall for the wedding."),

        new("Ms.", "Sneha Kapoor", "Rohit Menon", "9820011224", EventTypes.Reception,
            94, null, 300, EventSlots.Evening, "Reception",
            "Venue,Catering,Decor", MealPreferences.Mixed,
            900_000m, 1_400_000m, LeadSources.Referral, LeadStages.Contacted, LeadPriorities.High,
            "Bandra", "Referred by an earlier client."),

        new("Mr.", "Arjun Nair", null, "9820011225", EventTypes.Conference,
            41, 2, 220, EventSlots.FullDay, null,
            "Venue,Catering,SoundAndLight", MealPreferences.Mixed,
            600_000m, 800_000m, LeadSources.Website, LeadStages.Qualified, LeadPriorities.Medium,
            "Andheri East", "Annual sales conference, needs AV and breakout space."),

        new("Ms.", "Divya Rao", "Karthik Iyer", "9820011226", EventTypes.Sangeet,
            63, null, 350, EventSlots.Night, "Sangeet",
            "Venue,Decor,Entertainment,Photography", MealPreferences.Vegetarian,
            700_000m, 1_100_000m, LeadSources.SocialAds, LeadStages.SiteVisit, LeadPriorities.Hot,
            "Juhu", "Venue visit done at Marina Bay. Loved the poolside."),

        new("Mr.", "Kabir Sethi", null, "9820011227", EventTypes.AnnualDay,
            28, null, 500, EventSlots.Evening, null,
            "Venue,Catering,Entertainment,SoundAndLight", MealPreferences.NonVegetarian,
            1_200_000m, 1_600_000m, LeadSources.VendorPartner, LeadStages.Negotiation, LeadPriorities.Hot,
            "Powai", "Negotiating per-plate rate. Corporate PO terms."),

        new("Ms.", "Meera Joshi", "Aditya Rane", "9820011228", EventTypes.Wedding,
            210, 4, 600, EventSlots.Evening, "Mehendi,Haldi,Sangeet,Wedding,Reception",
            "FullPlanning", MealPreferences.Jain,
            3_000_000m, 4_200_000m, LeadSources.RepeatClient, LeadStages.FollowUp, LeadPriorities.High,
            "Worli", "Full planning mandate. Sister's wedding was with us in 2024."),

        new("Mrs.", "Radhika Bhatt", null, "9820011229", EventTypes.Birthday,
            17, null, 80, EventSlots.Afternoon, null,
            "Venue,Catering,Decor", MealPreferences.Vegetarian,
            180_000m, 260_000m, LeadSources.WalkIn, LeadStages.Contacted, LeadPriorities.Medium,
            "Dadar", "60th birthday, family only."),

        new("Mr.", "Imran Qureshi", "Fatima Sheikh", "9820011230", EventTypes.Engagement,
            55, null, 200, EventSlots.Evening, "Engagement",
            "Venue,Catering,Photography", MealPreferences.NonVegetarian,
            450_000m, 650_000m, LeadSources.EventPortal, LeadStages.Qualified, LeadPriorities.Medium,
            "Bandra", null),

        new("Ms.", "Ananya Deshmukh", "Rohit Gupta", "9820011231", EventTypes.DestinationWedding,
            285, 4, 250, EventSlots.FullDay, "Mehendi,Sangeet,Wedding",
            "FullPlanning,Accommodation,Transport", MealPreferences.Mixed,
            5_000_000m, 7_500_000m, LeadSources.Exhibition, LeadStages.New, LeadPriorities.Hot,
            "Goa", "Met at the wedding expo. Considering Goa or Udaipur."),

        new("Mr.", "Sanjay Pillai", null, "9820011232", EventTypes.ProductLaunch,
            22, null, 150, EventSlots.Evening, null,
            "Venue,Decor,SoundAndLight,Videography", MealPreferences.Mixed,
            500_000m, 700_000m, LeadSources.Website, LeadStages.ObmVisit, LeadPriorities.High,
            "Lower Parel", "Met at their office. Needs a stage and LED wall."),

        new("Mrs.", "Lata Krishnan", null, "9820011233", EventTypes.Anniversary,
            9, null, 120, EventSlots.Evening, null,
            "Venue,Catering,Decor,Entertainment", MealPreferences.Vegetarian,
            250_000m, 350_000m, LeadSources.Referral, LeadStages.Booked, LeadPriorities.Medium,
            "Chembur", "25th anniversary. Booking confirmed, advance received."),

        new("Mr.", "Rahul Verma", "Priya Nanda", "9820011234", EventTypes.Wedding,
            -12, 2, 400, EventSlots.Evening, "Sangeet,Wedding",
            "Venue,Catering,Decor", MealPreferences.Vegetarian,
            1_500_000m, 2_000_000m, LeadSources.EventPortal, LeadStages.Lost, LeadPriorities.Low,
            "Thane", "Lost — the date we had free did not suit the family."),
    ];

    public static async Task SeedAsync(AppDbContext db)
    {
        // One venue anywhere means this tenant is in use. Never add to it.
        if (await db.Projects.IgnoreQueryFilters().AnyAsync()) return;

        var company = await db.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync();
        if (company is null) return;

        var branch = await db.Branches
            .Where(b => b.CompanyId == company.Id)
            .OrderBy(b => b.Id)
            .FirstOrDefaultAsync();

        if (branch is null) return;

        var owner = await db.Users
            .Where(u => u.CompanyId == company.Id && u.Role != Roles.SuperAdmin)
            .OrderBy(u => u.Id)
            .FirstOrDefaultAsync();

        /* ---------------- venues and their spaces ---------------- */

        var venues = new List<Project>();

        foreach (var seed in Venues)
        {
            var venue = new Project
            {
                CompanyId = company.Id,
                Name = seed.Name,
                Code = seed.Code,
                Developer = seed.OperatedBy,
                Type = seed.Type,
                Status = VenueStatuses.Active,
                City = seed.City,
                Locality = seed.Locality,
                Address = $"{seed.Locality}, {seed.City}",
                Amenities = "Valet parking, Bridal suite, In-house kitchen, Green rooms, Generator backup",
                Description = $"{seed.Type} run by {seed.OperatedBy} in {seed.Locality}.",
                NoiseCurfew = new TimeSpan(22, 0, 0),
                AllowsOutsideCatering = seed.Type == VenueTypes.Resort,
                AllowsAlcohol = true,
                AllowsOpenFlame = true,
                GuestRooms = seed.Type == VenueTypes.Resort ? 68 : null,
                ParkingCapacity = 180,
            };

            db.Projects.Add(venue);
            venues.Add(venue);
        }

        await db.SaveChangesAsync();

        for (var v = 0; v < venues.Count; v++)
        {
            var venue = venues[v];
            var minPrice = decimal.MaxValue;
            var maxPrice = 0m;

            foreach (var space in Venues[v].Spaces)
            {
                // The indicative total is the rental plus catering at seated
                // capacity — the number a planner quotes on the phone before
                // anything is priced properly.
                var indicative = space.Rental + (space.PerPlate * space.Seating);
                minPrice = Math.Min(minPrice, indicative);
                maxPrice = Math.Max(maxPrice, indicative);

                db.Units.Add(new Unit
                {
                    CompanyId = company.Id,
                    ProjectId = venue.Id,
                    UnitNumber = space.Name,
                    Floor = 0,
                    Configuration = space.Type,
                    CarpetArea = space.Area,
                    BuiltUpArea = decimal.Round(space.Area * 1.2m, 0),
                    SuperArea = decimal.Round(space.Area * 1.4m, 0),
                    SeatingCapacity = space.Seating,
                    FloatingCapacity = space.Floating,
                    TheatreCapacity = (int)(space.Seating * 1.4),
                    Bathrooms = 2,
                    Balconies = space.Outdoor ? 0 : 1,
                    ParkingSlots = space.Seating / 4,
                    IsAirConditioned = !space.Outdoor,
                    IsOutdoor = space.Outdoor,
                    HasStage = true,
                    HasAttachedKitchen = !space.Outdoor,
                    Status = UnitStatuses.Available,
                    BasePrice = space.Rental,
                    PricePerSqft = decimal.Round(space.Rental / space.Area, 2),
                    TotalPrice = indicative,
                    PricePerPlate = space.PerPlate,
                    MinimumPlates = space.MinPlates,
                    PeakDatePremium = 0.20m,
                    SecurityDeposit = decimal.Round(space.Rental * 0.2m, 0),
                });
            }

            venue.PriceMin = minPrice == decimal.MaxValue ? null : minPrice;
            venue.PriceMax = maxPrice == 0m ? null : maxPrice;
        }

        await db.SaveChangesAsync();

        /* ---------------- enquiries ---------------- */

        var today = DateTime.UtcNow.Date;
        var created = new List<Lead>();

        foreach (var seed in Leads)
        {
            var eventDate = today.AddDays(seed.DaysOut);

            var lead = new Lead
            {
                CompanyId = company.Id,
                BranchId = branch.Id,
                Salutation = seed.Salutation,
                Name = seed.Name,
                PartnerName = seed.Partner,
                Phone = seed.Phone,
                Email = $"{seed.Name.Split(' ')[0].ToLowerInvariant()}@example.com",
                City = "Mumbai",
                Country = "India",

                Source = seed.Source,
                Stage = seed.Stage,
                Priority = seed.Priority,
                OwnerId = owner?.Id,
                Notes = seed.Notes,

                EventType = seed.EventType,
                EventCategory = EventTypes.CategoryOf(seed.EventType),
                EventDate = DateTime.SpecifyKind(eventDate, DateTimeKind.Utc),
                EventEndDate = seed.RunDays is int days
                    ? DateTime.SpecifyKind(eventDate.AddDays(days - 1), DateTimeKind.Utc)
                    : null,
                EventSlot = seed.Slot,
                GuestCount = seed.Guests,
                Functions = seed.Functions,
                ServicesNeeded = seed.Services,
                MealPreference = seed.Meal,
                PreferredLocality = seed.Locality,
                BudgetMin = seed.BudgetMin,
                BudgetMax = seed.BudgetMax,

                // A lead that has reached a visit has obviously been responded
                // to; leaving the SLA unmet on those would light the list up red
                // for no reason.
                CreatedAt = DateTime.UtcNow.AddDays(-Math.Min(30, Math.Abs(seed.DaysOut) / 4)),
                SlaDueAt = DateTime.UtcNow.AddHours(seed.Priority == LeadPriorities.Hot ? 1 : 8),
                FirstResponseAt = seed.Stage == LeadStages.New
                    ? null
                    : DateTime.UtcNow.AddDays(-1),
                LastActivityAt = DateTime.UtcNow.AddDays(-1),
            };

            db.Leads.Add(lead);
            created.Add(lead);
        }

        await db.SaveChangesAsync();

        foreach (var lead in created)
        {
            db.LeadActivities.Add(new LeadActivity
            {
                LeadId = lead.Id,
                Type = LeadActivityTypes.Created,
                Remarks = "Enquiry captured.",
                ActorId = owner?.Id ?? 0,
                ActorName = owner?.Name ?? "System",
                CreatedAt = lead.CreatedAt,
            });
        }

        await db.SaveChangesAsync();
    }
}
