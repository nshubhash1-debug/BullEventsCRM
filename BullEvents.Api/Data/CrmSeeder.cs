using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Data;

/// <summary>
/// Builds a workspace that behaves like a running business: real inventory,
/// a customer base, two years of deals with known outcomes, and the call,
/// visit, quotation and follow-up traffic that would have produced them.
///
/// Two reasons this is worth the code. First, dense list views are the only way
/// to tell whether filtering, faceting, sorting and paging actually hold up.
/// Second — and more importantly — the local models need labelled history:
/// closed deals to learn win probability from, dispositioned call notes to learn
/// sentiment from, and enough months of bookings for the forecaster to have a
/// series at all. Outcomes below are generated from the same signals a real
/// pipeline shows, then sampled with noise, so there is a genuine pattern to
/// learn rather than coin flips.
/// </summary>
public static class CrmSeeder
{
    private const int TargetContacts = 220;
    private const int TargetOpportunities = 260;

    private static readonly string[] FirstNames =
    [
        "Aarav", "Vivaan", "Aditya", "Vihaan", "Arjun", "Reyansh", "Krishna", "Ishaan",
        "Rohan", "Kabir", "Ananya", "Diya", "Aadhya", "Saanvi", "Meera", "Riya",
        "Neha", "Pooja", "Sneha", "Divya", "Rahul", "Amit", "Vikram", "Sanjay",
        "Karan", "Nikhil", "Priya", "Kavya", "Ishita", "Tanvi", "Manish", "Deepak",
        "Farhan", "Zoya", "Imran", "Ayesha", "Rajesh", "Suresh", "Anil", "Kiran",
        "Nandini", "Aditi", "Varun", "Siddharth", "Aisha", "Rehan", "Lakshmi", "Gaurav"
    ];

    private static readonly string[] LastNames =
    [
        "Sharma", "Verma", "Patel", "Nair", "Iyer", "Reddy", "Mehta", "Joshi",
        "Kapoor", "Malhotra", "Desai", "Chopra", "Sethi", "Rao", "Gupta", "Singh",
        "Bhatt", "Kulkarni", "Menon", "Shah", "Agarwal", "Bose", "Chauhan", "Pillai"
    ];

    private static readonly string[] Localities =
    [
        "Bandra West", "Powai", "Andheri East", "Lower Parel", "Worli", "Thane West",
        "Vashi", "Chembur", "Goregaon East", "Malad West", "Kharghar", "Wakad"
    ];

    private static readonly string[] Zones =
    [
        "MUMBAI WEST", "MUMBAI SOUTH", "NAVI MUMBAI", "THANE", "PUNE WEST", "PUNE EAST"
    ];

    private static readonly string[] MaritalStatuses = ["Married", "Single", "Divorced"];

    /// <summary>Branches sit in different states; the address should say so.</summary>
    private static string StateFor(string city) => city switch
    {
        "Gurugram" => "Haryana",
        "Delhi" => "Delhi",
        "Bengaluru" => "Karnataka",
        "Hyderabad" => "Telangana",
        _ => "Maharashtra",
    };

    private static readonly string[] Occupations =
    [
        "Salaried", "Self-employed", "Business owner", "Professional", "Retired", "NRI professional"
    ];

    private static readonly string[] Designations =
    [
        "Director", "Founder", "Senior Manager", "Consultant", "Partner",
        "Vice President", "Proprietor", "General Manager", "Architect", "Surgeon"
    ];

    private static readonly string[] Facings =
    [
        "North", "South", "East", "West", "North-East", "South-West"
    ];

    private static readonly string[] Views =
    [
        "Sea view", "Garden view", "City view", "Podium view", "Road facing", "Club view"
    ];

    private static readonly string[] PartnerNames =
    [
        "Skyline Realty Partners", "Metro Property Consultants", "Anchor Broking",
        "HDFC Home Loans", "Prime Estates LLP", "Urban Nest Advisors",
        "Kotak Realty Desk", "Greenfield Developers", "Coastal Housing Society",
        "Axis Property Services", "Nexus Corporate Housing", "Landmark Brokers"
    ];

    private static readonly string[] PositiveNotes =
    [
        "Very interested in the 3BHK, wants to book after the site visit.",
        "Loved the sea-facing unit — shortlisted two options.",
        "Loan sanctioned, ready to pay the token this week.",
        "Keen on the corner unit, asked us to hold it for 48 hours.",
        "Positive call, family visiting the site on the weekend.",
        "Agreed on pricing, will finalise once the agreement draft is shared.",
        "Excited about the possession timeline, wants a payment schedule.",
        "Shortlisted our project over two competitors, moving to booking."
    ];

    private static readonly string[] NegativeNotes =
    [
        "Not interested — budget mismatch, looking under 80 lakh.",
        "Found the pricing too costly compared to a nearby project.",
        "Loan rejected, has dropped the plan for now.",
        "Bought elsewhere last month, asked us not to call again.",
        "Over budget after the floor rise and PLC were added.",
        "Delayed possession is a problem, wants ready-to-move only.",
        "Wrong number — the lead data was incorrect.",
        "Postponed the purchase to next financial year."
    ];

    private static readonly string[] NeutralNotes =
    [
        "Discussed configurations, will revert after speaking to family.",
        "Asked for a brochure and floor plans over WhatsApp.",
        "Busy at work, asked to call back next week.",
        "Comparing two localities, no decision yet.",
        "Wants to understand the payment plan in detail.",
        "Requested a revised quotation with a different unit."
    ];

    private static readonly string[] NoteOpeners =
    [
        "Spoke to the client.", "Quick catch-up.", "Called after the site visit.",
        "Followed up on WhatsApp.", "Met at the office.", "Returned their missed call.",
        "Long conversation today.", "Short call between meetings."
    ];

    private static readonly string[] NoteClosers =
    [
        "Will update the record after the next touchpoint.",
        "Owner informed.",
        "Noted for the weekly review.",
        "Nothing pending from our side right now.",
        "Sent a summary over email.",
        "Adding a reminder for next week."
    ];

    /// <summary>
    /// Composes a note from an opener, a sentiment-bearing core and a closer.
    ///
    /// Drawing straight from a fixed pool would give every note a duplicate in
    /// the training set, and the sentiment model would score a meaningless 100%
    /// by memorising sixteen strings. Free text in the wild varies; so does this.
    /// </summary>
    private static string Note(Random random, string[] core) =>
        string.Join(' ', new[]
        {
            random.NextDouble() < 0.65 ? NoteOpeners[random.Next(NoteOpeners.Length)] : null,
            core[random.Next(core.Length)],
            random.NextDouble() < 0.5 ? NoteClosers[random.Next(NoteClosers.Length)] : null,
        }.Where(part => part is not null));

    /// <summary>
    /// Rounds to the nearest multiple — thousands for prices, lakhs for budgets.
    /// <c>decimal.Round</c> only takes non-negative precision, and real quoted
    /// prices are round numbers, not exact arithmetic.
    /// </summary>
    private static decimal RoundTo(decimal value, decimal step) =>
        step <= 0 ? value : Math.Round(value / step, MidpointRounding.AwayFromZero) * step;

    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Projects.AnyAsync()) return;

        // Ordered: the home tenant is the first one created, and on a
        // database carrying several, an unordered First is a coin toss about
        // which company this project lands in.
        var company = await db.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync();
        if (company is null) return;

        var branches = await db.Branches.Where(b => b.CompanyId == company.Id).ToListAsync();
        if (branches.Count == 0) return;

        var users = await db.Users.Where(u => u.CompanyId == company.Id && u.IsActive).ToListAsync();
        if (users.Count == 0) return;

        // Fixed seed: the demo workspace looks the same on every fresh database,
        // which makes screenshots and bug reports reproducible.
        var random = new Random(20260822);
        var now = DateTime.UtcNow;

        var projects = await SeedInventoryAsync(db, company.Id, random);
        var units = await db.Units.ToListAsync();

        var leads = await SeedLiveLeadsAsync(db, company.Id, branches, users, random, now);

        var contacts = await SeedContactsAsync(db, company.Id, branches, users, random, now);
        var opportunities = await SeedOpportunitiesAsync(
            db, company.Id, branches, users, contacts, projects, units, random, now);

        await SeedQuotationsAsync(db, company.Id, branches, users, opportunities, contacts, random, now);
        await SeedSiteVisitsAsync(db, company.Id, branches, users, contacts, projects, units, random, now);
        await SeedCallsAsync(db, company.Id, branches, users, contacts, random, now);
        await SeedObmVisitsAsync(db, company.Id, branches, users, leads, random, now);
        await SeedFollowUpsAsync(db, company.Id, branches, users, contacts, opportunities, random, now);
    }

    /* ------------------------------------------------------------------ *
     * Live lead pipeline
     * ------------------------------------------------------------------ */

    /// <summary>
    /// The history seeder leaves a workspace that is almost entirely closed
    /// leads — good for training, useless for looking at. This adds the open
    /// pipeline a working desk would have: every stage represented, budgets and
    /// requirements filled in, campaign attribution set, and SLA clocks in all
    /// three states so the list view has something real to filter and sort.
    /// </summary>
    private static async Task<List<Lead>> SeedLiveLeadsAsync(
        AppDbContext db,
        int companyId,
        List<Branch> branches,
        List<User> users,
        Random random,
        DateTime now)
    {
        var openStages = new[]
        {
            LeadStages.New, LeadStages.Contacted, LeadStages.SiteVisit, LeadStages.Negotiation
        };

        var campaigns = new[]
        {
            "Diwali Launch 2026", "Sea-facing Premium", "NRI Outreach Q3",
            "Portal Retargeting", "Referral Rewards", "Weekend Open House",
        };

        var utmSources = new[] { "google", "meta", "linkedin", "direct", "portal", "referral" };
        var subStatuses = new[]
        {
            "Awaiting callback", "Budget review", "Menu shortlisting", "Comparing venues",
            "Family decision pending", "Date not fixed", "Ready to visit",
        };
        var utmMediums = new[] { "cpc", "organic", "email", "social", "affiliate" };
        var requirements = EventTypes.All;
        var paymentModes = PaymentPreferences.All;
        var mealPreferences = MealPreferences.All;
        var slots = EventSlots.All;

        // Weighted towards the combinations a planner actually gets asked for:
        // venue plus catering plus décor is the standard wedding brief.
        var serviceSets = new[]
        {
            "Venue,Catering,Decor",
            "Venue,Catering,Decor,Photography",
            "FullPlanning",
            "Decor,Photography,Entertainment",
            "Venue,Catering",
            "Catering,Decor,SoundAndLight,Makeup",
            "Photography,Videography",
        };

        var functionSets = new[]
        {
            "Wedding", "Wedding,Reception", "Mehendi,Haldi,Sangeet,Wedding",
            "Mehendi,Sangeet,Wedding,Reception", "Engagement", "Reception",
        };

        var tagPool = new[] { "nri", "urgent", "destination", "repeat-client", "referral", "walk-in" };

        // Whoever outranks a sales agent can be a supporting manager; on a
        // company that has none, nobody is offered rather than a random peer.
        var managers = users
            .Where(u => u.Role is Roles.BranchManager or Roles.TeamLead or Roles.CompanyAdmin)
            .ToList();

        var leads = new List<Lead>();
        var activities = new List<(Lead Lead, LeadActivity Activity)>();

        for (var i = 0; i < 380; i++)
        {
            var first = FirstNames[random.Next(FirstNames.Length)];
            var last = LastNames[random.Next(LastNames.Length)];
            var branch = branches[random.Next(branches.Count)];

            // A live desk always has a few unclaimed leads; that is what the
            // auto-assignment view exists to clear.
            var owner = random.NextDouble() < 0.12 ? null : users[random.Next(users.Count)];

            // Managers back the owner on the bigger deals, so only some leads
            // carry one — and never the owner themselves.
            var supportingManager = owner is not null && random.NextDouble() < 0.45
                ? managers.FirstOrDefault(m => m.Id != owner.Id)
                : null;

            var priority = LeadPriorities.All[random.Next(LeadPriorities.All.Length)];
            var source = LeadSources.All[random.Next(LeadSources.All.Length)];
            var createdAt = now.AddDays(-random.Next(0, 90)).AddHours(-random.Next(0, 24));

            var budgetMin = RoundTo((decimal)(random.Next(40, 280) * 100_000), 100_000m);

            var eventType = requirements[random.Next(requirements.Length)];
            var isMultiDay = EventTypes.WeddingFunctions.Contains(eventType)
                && random.NextDouble() < 0.55;

            // Spread over the next ten months, with one in twelve still
            // undated — the families waiting on a muhurat, whom the pipeline
            // has to render without a date.
            var eventDate = random.NextDouble() < 0.08
                ? (DateTime?)null
                : now.Date.AddDays(random.Next(7, 300));

            // Hot leads owe a response within the hour, everything else within
            // the working day — so the SLA column shows all three states.
            var slaDueAt = createdAt.AddHours(priority == LeadPriorities.Hot ? 1 : 8);
            var responded = random.NextDouble() < 0.62;

            var lead = new Lead
            {
                CompanyId = companyId,
                BranchId = branch.Id,
                Salutation = random.NextDouble() < 0.6 ? "Mr." : "Ms.",
                Name = $"{first} {last}",
                CompanyName = random.NextDouble() < 0.3
                    ? $"{last} {new[] { "Enterprises", "Exports", "Ventures", "Consulting" }[random.Next(4)]}"
                    : null,
                Phone = $"98{random.Next(10000000, 99999999)}",
                Phone2 = random.NextDouble() < 0.25 ? $"77{random.Next(10000000, 99999999)}" : null,
                Email = $"{first.ToLowerInvariant()}.{last.ToLowerInvariant()}.l{i}@example.com",
                City = branch.City,
                State = StateFor(branch.City),
                Pincode = $"4000{random.Next(10, 99)}",
                Country = "India",
                Zone = Zones[random.Next(Zones.Length)],
                Address = $"{Localities[random.Next(Localities.Length)]}, {branch.City}",
                DateOfBirth = random.NextDouble() < 0.55
                    ? new DateTime(random.Next(1965, 2000), random.Next(1, 13), random.Next(1, 28),
                        0, 0, 0, DateTimeKind.Utc)
                    : null,
                AnniversaryDate = random.NextDouble() < 0.3
                    ? new DateTime(random.Next(1995, 2024), random.Next(1, 13), random.Next(1, 28),
                        0, 0, 0, DateTimeKind.Utc)
                    : null,
                MaritalStatus = random.NextDouble() < 0.7
                    ? MaritalStatuses[random.Next(MaritalStatuses.Length)]
                    : null,
                FatherOrSpouseName = random.NextDouble() < 0.4
                    ? $"{FirstNames[random.Next(FirstNames.Length)]} {last}"
                    : null,
                Occupation = Occupations[random.Next(Occupations.Length)],
                Designation = Designations[random.Next(Designations.Length)],
                Nationality = random.NextDouble() < 0.9 ? "Indian" : "NRI",
                Source = source,
                Stage = openStages[random.Next(openStages.Length)],
                Priority = priority,
                OwnerId = owner?.Id,
                SupportingManagerId = supportingManager?.Id,
                Notes = Note(random, random.NextDouble() < 0.5 ? NeutralNotes : PositiveNotes),
                BudgetMin = budgetMin,
                BudgetMax = RoundTo(budgetMin * (decimal)(1.15 + (random.NextDouble() * 0.6)), 100_000m),
                EventType = eventType,
                EventCategory = EventTypes.CategoryOf(eventType),

                // Nearly every enquiry has a date; the handful that do not are
                // the families still waiting on a muhurat, and they are worth
                // seeding because the pipeline has to render them.
                EventDate = eventDate,
                EventEndDate = eventDate is null || !isMultiDay
                    ? null
                    : eventDate.Value.AddDays(random.Next(1, 4)),
                EventSlot = slots[random.Next(slots.Length)],
                IsDateFlexible = random.NextDouble() < 0.25,
                GuestCount = new[] { 80, 150, 250, 350, 500, 750, 1000 }[random.Next(7)],
                Functions = EventTypes.WeddingFunctions.Contains(eventType)
                    ? functionSets[random.Next(functionSets.Length)]
                    : null,
                ServicesNeeded = serviceSets[random.Next(serviceSets.Length)],
                MealPreference = mealPreferences[random.Next(mealPreferences.Length)],
                PreferredLocality = Localities[random.Next(Localities.Length)],
                PaymentMode = paymentModes[random.Next(paymentModes.Length)],
                SubStatus = random.NextDouble() < 0.6
                    ? subStatuses[random.Next(subStatuses.Length)]
                    : null,
                Campaign = random.NextDouble() < 0.75 ? campaigns[random.Next(campaigns.Length)] : null,
                UtmSource = utmSources[random.Next(utmSources.Length)],
                UtmMedium = utmMediums[random.Next(utmMediums.Length)],
                ReferredBy = source == LeadSources.Referral
                    ? $"{FirstNames[random.Next(FirstNames.Length)]} {LastNames[random.Next(LastNames.Length)]}"
                    : null,
                Tags = random.NextDouble() < 0.45
                    ? string.Join(',', tagPool.OrderBy(_ => random.Next()).Take(random.Next(1, 3)))
                    : null,
                SlaDueAt = slaDueAt,
                FirstResponseAt = responded
                    ? createdAt.AddMinutes(random.Next(5, 900))
                    : null,
                LastActivityAt = now.AddDays(-random.Next(0, 25)),
                CreatedAt = createdAt,
                UpdatedAt = now.AddDays(-random.Next(0, 10)),
            };

            leads.Add(lead);
        }

        db.Leads.AddRange(leads);
        await db.SaveChangesAsync();

        // A timeline per lead, so the record page and the activity-count column
        // are not empty on a freshly seeded workspace.
        var actor = users[0];

        foreach (var lead in leads)
        {
            activities.Add((lead, new LeadActivity
            {
                LeadId = lead.Id,
                Type = LeadActivityTypes.Created,
                Remarks = $"Lead captured from {lead.Source}.",
                ActorId = actor.Id,
                ActorName = actor.Name,
                CreatedAt = lead.CreatedAt,
            }));

            if (lead.FirstResponseAt is DateTime respondedAt)
            {
                activities.Add((lead, new LeadActivity
                {
                    LeadId = lead.Id,
                    Type = LeadActivityTypes.Call,
                    Remarks = Note(random, NeutralNotes),
                    ActorId = actor.Id,
                    ActorName = actor.Name,
                    CreatedAt = respondedAt,
                }));
            }

            if (lead.Stage != LeadStages.New)
            {
                activities.Add((lead, new LeadActivity
                {
                    LeadId = lead.Id,
                    Type = LeadActivityTypes.StageChange,
                    FromStage = LeadStages.New,
                    ToStage = lead.Stage,
                    ActorId = actor.Id,
                    ActorName = actor.Name,
                    CreatedAt = lead.LastActivityAt ?? lead.CreatedAt,
                }));
            }
        }

        db.LeadActivities.AddRange(activities.Select(a => a.Activity));
        await db.SaveChangesAsync();

        return leads;
    }

    /* ------------------------------------------------------------------ *
     * Inventory
     * ------------------------------------------------------------------ */

    private static async Task<List<Project>> SeedInventoryAsync(
        AppDbContext db,
        int companyId,
        Random random)
    {
        var definitions = new[]
        {
            ("The Grand Palladium", "GPL", "Palladium Hospitality", VenueTypes.BanquetHall,
                VenueStatuses.Active, "Mumbai", "Lower Parel", 24000m, 2, 3),
            ("Marina Bay Resort", "MBR", "Coastal Hospitality", VenueTypes.Resort,
                VenueStatuses.Active, "Mumbai", "Worli", 38000m, 2, 2),
            ("Emerald Convention Centre", "ECC", "Emerald Group", VenueTypes.ConventionCentre,
                VenueStatuses.Active, "Mumbai", "Andheri East", 19500m, 2, 2),
            ("Palm Grove Lawns", "PGL", "Greenfield Estates", VenueTypes.Lawn,
                VenueStatuses.Seasonal, "Pune", "Wakad", 12500m, 1, 1),
        };

        var projects = new List<Project>();

        foreach (var (name, code, developer, type, status, city, locality, rate, towerCount, floors)
            in definitions)
        {
            var project = new Project
            {
                CompanyId = companyId,
                Name = name,
                Code = code,
                Developer = developer,
                Type = type,
                Status = status,
                City = city,
                Locality = locality,
                Address = $"{locality}, {city}",
                ReraNumber = $"VEN5180000{random.Next(1000, 9999)}",
                LaunchDate = DateTime.UtcNow.AddMonths(-random.Next(12, 36)),
                Amenities = "Valet parking, Bridal suite, In-house kitchen, Green rooms, Generator backup, 24x7 security",
                Description = $"{type} run by {developer} in {locality}.",

                // The operating rules that decide a booking, and the reason
                // planners phone the venue before they phone the client back.
                NoiseCurfew = new TimeSpan(22, 0, 0),
                AllowsOutsideCatering = random.NextDouble() < 0.4,
                AllowsAlcohol = random.NextDouble() < 0.7,
                AllowsOpenFlame = random.NextDouble() < 0.6,
                GuestRooms = type == VenueTypes.Resort ? random.Next(30, 120) : null,
                ParkingCapacity = random.Next(60, 400),
            };

            db.Projects.Add(project);
            projects.Add(project);
        }

        await db.SaveChangesAsync();

        // Seating is deliberately well under floating capacity — round tables
        // and a dance floor cost roughly half the standing count, and a venue
        // that quotes the floating number is how a wedding ends up with guests
        // in the corridor.
        var indoorSpaces = new (string Config, decimal Area, int Seating, int Floating, bool Outdoor)[]
        {
            ("BanquetHall", 3200m, 250, 450, false),
            ("Ballroom", 6500m, 550, 900, false),
            ("DiningHall", 2200m, 180, 300, false),
            ("ConferenceRoom", 1400m, 120, 200, false),
            ("PreFunctionArea", 900m, 60, 150, false),
        };

        var outdoorSpaces = new (string Config, decimal Area, int Seating, int Floating, bool Outdoor)[]
        {
            ("Lawn", 12000m, 700, 1200, true),
            ("Poolside", 4500m, 250, 450, true),
            ("Terrace", 3000m, 180, 320, true),
            ("Courtyard", 2600m, 160, 280, true),
            ("MandapArea", 5000m, 300, 500, true),
        };

        for (var p = 0; p < projects.Count; p++)
        {
            var project = projects[p];
            var (_, _, _, type, _, _, _, rate, towerCount, floors) = definitions[p];

            var palette = type is VenueTypes.Lawn or VenueTypes.Resort
                ? outdoorSpaces
                : indoorSpaces;

            var minPrice = decimal.MaxValue;
            var maxPrice = 0m;

            for (var t = 0; t < towerCount; t++)
            {
                var tower = new Tower
                {
                    ProjectId = project.Id,
                    Name = $"Block {(char)('A' + t)}",
                    FloorCount = floors,
                    UnitsPerFloor = 4,
                    Status = project.Status,
                };

                db.Towers.Add(tower);
                await db.SaveChangesAsync();

                for (var floor = 1; floor <= floors; floor++)
                {
                    for (var slot = 1; slot <= 4; slot++)
                    {
                        var spec = palette[random.Next(palette.Length)];

                        // Hall rental scales with the area it takes over; the
                        // per-plate rate is the bigger half of the bill and is
                        // set by the venue's tier, not by the room's size.
                        var rental = RoundTo(spec.Area * (rate / 200m), 5_000m);
                        var perPlate = RoundTo(rate / 20m, 50m);
                        var isCorner = slot is 1 or 4;
                        var peakPremium = isCorner ? 0.25m : 0.15m;
                        var total = rental + (perPlate * spec.Seating);

                        minPrice = Math.Min(minPrice, total);
                        maxPrice = Math.Max(maxPrice, total);

                        // A space is in the catalogue unless the venue has taken
                        // it out. Whether it is free on a given date is a
                        // SpaceBooking question, not a status.
                        var status = random.NextDouble() switch
                        {
                            < 0.05 => UnitStatuses.Blocked,
                            < 0.08 => UnitStatuses.NotForSale,
                            _ => UnitStatuses.Available,
                        };

                        db.Units.Add(new Unit
                        {
                            CompanyId = companyId,
                            ProjectId = project.Id,
                            TowerId = tower.Id,
                            UnitNumber = $"{spec.Config} {(char)('A' + t)}{slot}",
                            Floor = floor,
                            Configuration = spec.Config,
                            CarpetArea = spec.Area,
                            BuiltUpArea = decimal.Round(spec.Area * 1.2m, 0),
                            SuperArea = decimal.Round(spec.Area * 1.4m, 0),
                            SeatingCapacity = spec.Seating,
                            FloatingCapacity = spec.Floating,
                            TheatreCapacity = (int)(spec.Seating * 1.4),
                            Facing = Facings[random.Next(Facings.Length)],
                            ViewType = Views[random.Next(Views.Length)],
                            Bathrooms = 2,
                            Balconies = spec.Outdoor ? 0 : 1,
                            ParkingSlots = spec.Seating / 4,
                            IsCornerUnit = isCorner,
                            VastuCompliant = random.NextDouble() > 0.25,
                            IsAirConditioned = !spec.Outdoor,
                            IsOutdoor = spec.Outdoor,
                            HasStage = random.NextDouble() < 0.6,
                            HasAttachedKitchen = random.NextDouble() < 0.45,
                            Status = status,
                            BasePrice = rental,
                            PricePerSqft = rate / 200m,
                            TotalPrice = total,
                            PricePerPlate = perPlate,
                            MinimumPlates = (int)(spec.Seating * 0.7),
                            PeakDatePremium = peakPremium,
                            SecurityDeposit = RoundTo(rental * 0.2m, 5_000m),
                            BlockReason = status switch
                            {
                                UnitStatuses.Blocked => "Renovation in progress",
                                UnitStatuses.NotForSale => "Retained for house use",
                                _ => null,
                            },
                        });
                    }
                }
            }

            project.PriceMin = minPrice == decimal.MaxValue ? null : minPrice;
            project.PriceMax = maxPrice == 0m ? null : maxPrice;
        }

        await db.SaveChangesAsync();
        return projects;
    }

    /* ------------------------------------------------------------------ *
     * Contacts
     * ------------------------------------------------------------------ */

    private static async Task<List<Contact>> SeedContactsAsync(
        AppDbContext db,
        int companyId,
        List<Branch> branches,
        List<User> users,
        Random random,
        DateTime now)
    {
        var contacts = new List<Contact>();

        for (var i = 0; i < TargetContacts; i++)
        {
            var first = FirstNames[random.Next(FirstNames.Length)];
            var last = LastNames[random.Next(LastNames.Length)];
            var branch = branches[random.Next(branches.Count)];
            var owner = users[random.Next(users.Count)];

            var createdAt = now.AddDays(-random.Next(10, 730));
            var dealCount = random.NextDouble() switch
            {
                < 0.55 => 0,
                < 0.85 => 1,
                < 0.96 => 2,
                _ => 3,
            };

            var budgetMin = RoundTo((decimal)(random.Next(45, 260) * 100_000), 100_000m);
            var lifetimeValue = dealCount == 0
                ? 0m
                : RoundTo(budgetMin * dealCount * (decimal)(0.85 + (random.NextDouble() * 0.4)), 1_000m);

            var lifecycle = dealCount switch
            {
                >= 2 => LifecycleStages.Repeat,
                1 => LifecycleStages.Customer,
                _ => random.NextDouble() < 0.4 ? LifecycleStages.Engaged : LifecycleStages.Prospect,
            };

            // Older contacts with nothing recent have genuinely lapsed — the
            // segmentation model needs that spread to find a "dormant" cluster.
            var lastActivity = random.NextDouble() < 0.3
                ? createdAt.AddDays(random.Next(1, 30))
                : now.AddDays(-random.Next(0, 90));

            if (lastActivity < now.AddDays(-180)) lifecycle = LifecycleStages.Dormant;

            contacts.Add(new Contact
            {
                CompanyId = companyId,
                BranchId = branch.Id,
                Salutation = random.NextDouble() < 0.6 ? "Mr." : "Ms.",
                FirstName = first,
                LastName = last,
                FullName = $"{first} {last}",
                Designation = Designations[random.Next(Designations.Length)],
                AccountName = random.NextDouble() < 0.35 ? $"{last} {LastNames[random.Next(LastNames.Length)]} LLP" : null,
                Phone = $"98{random.Next(10000000, 99999999)}",
                Email = $"{first.ToLowerInvariant()}.{last.ToLowerInvariant()}{i}@example.com",
                WhatsAppNumber = $"98{random.Next(10000000, 99999999)}",
                City = branch.City,
                State = "Maharashtra",
                Country = "India",
                Pincode = $"4000{random.Next(10, 99)}",
                Type = dealCount > 0 ? ContactTypes.Customer : ContactTypes.Contact,
                LifecycleStage = lifecycle,
                Source = LeadSources.All[random.Next(LeadSources.All.Length)],
                Tags = random.NextDouble() < 0.3 ? "nri" : null,
                LifetimeValue = lifetimeValue,
                DealCount = dealCount,
                BudgetMin = budgetMin,
                BudgetMax = RoundTo(budgetMin * (decimal)(1.2 + random.NextDouble()), 100_000m),
                PreferredConfiguration = SpaceTypes.All[random.Next(1, 8)],
                PreferredLocality = Localities[random.Next(Localities.Length)],
                DoNotCall = random.NextDouble() < 0.04,
                DoNotEmail = random.NextDouble() < 0.06,
                WhatsAppOptIn = random.NextDouble() > 0.12,
                PanNumber = random.NextDouble() < 0.5 ? $"ABCDE{random.Next(1000, 9999)}F" : null,
                OwnerId = owner.Id,
                LastActivityAt = lastActivity,
                CreatedAt = createdAt,
                UpdatedAt = lastActivity,
            });
        }

        db.Contacts.AddRange(contacts);
        await db.SaveChangesAsync();
        return contacts;
    }

    /* ------------------------------------------------------------------ *
     * Opportunities
     * ------------------------------------------------------------------ */

    private static async Task<List<Opportunity>> SeedOpportunitiesAsync(
        AppDbContext db,
        int companyId,
        List<Branch> branches,
        List<User> users,
        List<Contact> contacts,
        List<Project> projects,
        List<Unit> units,
        Random random,
        DateTime now)
    {
        var opportunities = new List<Opportunity>();

        for (var i = 0; i < TargetOpportunities; i++)
        {
            var contact = contacts[random.Next(contacts.Count)];
            var project = projects[random.Next(projects.Count)];
            var branch = branches[random.Next(branches.Count)];
            var owner = users[random.Next(users.Count)];

            var projectUnits = units.Where(u => u.ProjectId == project.Id).ToList();
            var unit = projectUnits.Count > 0 ? projectUnits[random.Next(projectUnits.Count)] : null;

            var createdAt = now.AddDays(-random.Next(5, 700));
            var amount = unit?.TotalPrice ?? RoundTo((decimal)(random.Next(60, 320) * 100_000), 10_000m);

            // Three quarters of the history is closed so the win model has
            // labelled outcomes; the rest is live pipeline for the board.
            var isClosed = createdAt < now.AddDays(-70) && random.NextDouble() < 0.78;

            var siteVisits = random.Next(0, 4);
            var quotations = random.Next(0, 3);
            var calls = random.Next(0, 12);

            // How far the deal travelled follows from how much work went into
            // it, and the outcome follows from how far it travelled. Generating
            // it in that order gives the model a causal pattern to find instead
            // of an artefact — and, critically, keeps the label out of the
            // features: for a closed deal the outcome lives in Stage while the
            // progression lives in LastOpenStage.
            var progression = Math.Clamp(
                (siteVisits * 0.9) + (quotations * 0.8) + (calls * 0.08)
                + ((random.NextDouble() - 0.5) * 1.4),
                0, 3.99);

            var openStage = OpportunityStages.Open[(int)progression];

            var winChance =
                0.06
                + (Array.IndexOf(OpportunityStages.Open, openStage) * 0.14)
                + (siteVisits * 0.07)
                + (quotations * 0.05)
                + (calls * 0.008)
                + (contact.DealCount > 0 ? 0.10 : 0)
                + (amount > 20_000_000m ? -0.08 : 0.03);

            winChance = Math.Clamp(winChance + ((random.NextDouble() - 0.5) * 0.20), 0.03, 0.94);

            var stage = isClosed
                ? random.NextDouble() < winChance ? OpportunityStages.ClosedWon : OpportunityStages.ClosedLost
                : openStage;

            var closedAt = isClosed ? createdAt.AddDays(random.Next(20, 120)) : (DateTime?)null;
            if (closedAt > now) closedAt = now.AddDays(-random.Next(1, 30));

            var probability = OpportunityStages.DefaultProbability(stage);

            opportunities.Add(new Opportunity
            {
                CompanyId = companyId,
                BranchId = branch.Id,
                Name = $"{contact.FullName} — {project.Name} {unit?.Configuration ?? "unit"}",
                ContactId = contact.Id,
                ProjectId = project.Id,
                UnitId = unit?.Id,
                Stage = stage,
                Type = OpportunityTypes.All[random.Next(OpportunityTypes.All.Length)],
                Source = contact.Source,
                ForecastCategory = ForecastCategories.ForStage(stage, probability),
                Amount = amount,
                ExpectedCommission = RoundTo(amount * 0.02m, 100m),
                Probability = probability,
                ExpectedCloseDate = (closedAt ?? now).AddDays(random.Next(-10, 60)),
                ActualCloseDate = closedAt,
                OwnerId = owner.Id,
                NextStep = stage switch
                {
                    OpportunityStages.Qualification => "Confirm budget and timeline",
                    OpportunityStages.NeedsAnalysis => "Shortlist two units and schedule a visit",
                    OpportunityStages.Proposal => "Share the revised quotation",
                    OpportunityStages.Negotiation => "Agree final price and payment plan",
                    _ => null,
                },
                NextStepDueAt = OpportunityStages.IsClosed(stage) ? null : now.AddDays(random.Next(-6, 18)),
                LossReason = stage == OpportunityStages.ClosedLost
                    ? new[] { "Budget mismatch", "Chose a competitor", "Loan rejected", "Postponed", "Location mismatch" }
                        [random.Next(5)]
                    : null,
                CompetitorName = stage == OpportunityStages.ClosedLost && random.NextDouble() < 0.5
                    ? new[] { "Lodha", "Godrej Properties", "Oberoi Realty", "Hiranandani" }[random.Next(4)]
                    : null,
                LastOpenStage = isClosed ? openStage : null,
                // Time spent in the last open stage before closing — set to the
                // close date it would read as zero for every training row, and
                // the feature would carry no information.
                StageEnteredAt = OpportunityStages.IsClosed(stage)
                    ? (closedAt ?? createdAt).AddDays(-random.Next(4, 45))
                    : now.AddDays(-random.Next(1, 55)),
                CreatedAt = createdAt,
                UpdatedAt = closedAt ?? now.AddDays(-random.Next(0, 20)),
            });
        }

        db.Opportunities.AddRange(opportunities);
        await db.SaveChangesAsync();
        return opportunities;
    }

    /* ------------------------------------------------------------------ *
     * Quotations
     * ------------------------------------------------------------------ */

    private static async Task SeedQuotationsAsync(
        AppDbContext db,
        int companyId,
        List<Branch> branches,
        List<User> users,
        List<Opportunity> opportunities,
        List<Contact> contacts,
        Random random,
        DateTime now)
    {
        var contactsById = contacts.ToDictionary(c => c.Id);
        var quotations = new List<Quotation>();

        foreach (var opportunity in opportunities.Where(_ => random.NextDouble() < 0.45))
        {
            var contact = opportunity.ContactId is int id ? contactsById.GetValueOrDefault(id) : null;
            var issuedAt = opportunity.CreatedAt.AddDays(random.Next(3, 40));
            if (issuedAt > now) issuedAt = now.AddDays(-random.Next(1, 10));

            var status = opportunity.Stage switch
            {
                OpportunityStages.ClosedWon => QuotationStatuses.Accepted,
                OpportunityStages.ClosedLost => QuotationStatuses.Rejected,
                OpportunityStages.Negotiation => QuotationStatuses.Negotiation,
                OpportunityStages.Proposal => QuotationStatuses.Sent,
                _ => QuotationStatuses.Draft,
            };

            var basePrice = opportunity.Amount;

            var quotation = new Quotation
            {
                CompanyId = companyId,
                BranchId = opportunity.BranchId,
                Title = $"Proposal — {opportunity.Name}",
                ContactId = opportunity.ContactId,
                OpportunityId = opportunity.Id,
                ProjectId = opportunity.ProjectId,
                UnitId = opportunity.UnitId,
                CustomerName = contact?.FullName ?? "Customer",
                CustomerEmail = contact?.Email,
                CustomerPhone = contact?.Phone,
                BillingAddress = contact is null ? null : $"{contact.City}, {contact.State}",
                Status = status,
                IssueDate = issuedAt,
                ValidUntil = issuedAt.AddDays(random.Next(7, 30)),
                SentAt = status == QuotationStatuses.Draft ? null : issuedAt.AddHours(random.Next(1, 48)),
                RespondedAt = status is QuotationStatuses.Accepted or QuotationStatuses.Rejected
                    ? issuedAt.AddDays(random.Next(2, 25))
                    : null,
                DiscountPercent = random.NextDouble() < 0.4 ? random.Next(1, 6) : 0m,
                TaxPercent = 5m,
                PaymentTerms = "20% on booking, 60% linked to construction, 20% on possession.",
                Notes = "Prices are indicative and subject to final agreement.",
                TermsAndConditions = "Stamp duty, registration and GST are payable at actuals.",
                RejectionReason = status == QuotationStatuses.Rejected ? opportunity.LossReason : null,
                OwnerId = opportunity.OwnerId ?? users[0].Id,
                QuoteNumber = Controllers.CrmControllerBase.ReferenceCode("QT", issuedAt),
                CreatedAt = issuedAt,
                UpdatedAt = issuedAt,
            };

            quotation.Lines.Add(Line("Unit consideration", "Base", 1m, basePrice, 0));
            quotation.Lines.Add(Line("Floor rise premium", "Premium", 1m,
                RoundTo(basePrice * 0.02m, 100m), 1));
            quotation.Lines.Add(Line("Covered car parking", "Amenity", random.Next(1, 3),
                350_000m, 2));
            quotation.Lines.Add(Line("Clubhouse membership", "Amenity", 1m, 250_000m, 3));

            if (random.NextDouble() < 0.4)
            {
                quotation.Lines.Add(Line("Interior package", "Optional", 1m,
                    RoundTo(basePrice * 0.05m, 100m), 4));
            }

            Recalculate(quotation);
            quotations.Add(quotation);
        }

        db.Quotations.AddRange(quotations);
        await db.SaveChangesAsync();

        static QuotationLine Line(string description, string category, decimal quantity, decimal price, int order) =>
            new()
            {
                Description = description,
                Category = category,
                Quantity = quantity,
                Unit = "no.",
                UnitPrice = price,
                SortOrder = order,
            };
    }

    private static void Recalculate(Quotation quotation)
    {
        foreach (var line in quotation.Lines)
        {
            line.LineTotal = decimal.Round(
                line.Quantity * line.UnitPrice * (1 - (line.DiscountPercent / 100m)), 2);
        }

        quotation.Subtotal = decimal.Round(quotation.Lines.Sum(l => l.LineTotal), 2);
        quotation.DiscountAmount = decimal.Round(quotation.Subtotal * quotation.DiscountPercent / 100m, 2);

        var taxable = quotation.Subtotal - quotation.DiscountAmount;
        quotation.TaxAmount = decimal.Round(taxable * quotation.TaxPercent / 100m, 2);
        quotation.Total = decimal.Round(taxable + quotation.TaxAmount, 2);
    }

    /* ------------------------------------------------------------------ *
     * Site visits
     * ------------------------------------------------------------------ */

    private static async Task SeedSiteVisitsAsync(
        AppDbContext db,
        int companyId,
        List<Branch> branches,
        List<User> users,
        List<Contact> contacts,
        List<Project> projects,
        List<Unit> units,
        Random random,
        DateTime now)
    {
        var leads = await db.Leads.Select(l => new { l.Id, l.Name, l.Phone, l.BranchId }).Take(400).ToListAsync();
        var visits = new List<SiteVisit>();

        for (var i = 0; i < 320; i++)
        {
            var useLead = leads.Count > 0 && random.NextDouble() < 0.55;
            var lead = useLead ? leads[random.Next(leads.Count)] : null;
            var contact = useLead ? null : contacts[random.Next(contacts.Count)];

            var project = projects[random.Next(projects.Count)];
            var projectUnits = units.Where(u => u.ProjectId == project.Id).ToList();
            var unit = projectUnits.Count > 0 ? projectUnits[random.Next(projectUnits.Count)] : null;

            var host = users[random.Next(users.Count)];
            var scheduledAt = now.AddDays(random.Next(-240, 21)).Date.AddHours(random.Next(10, 19));
            var isPast = scheduledAt < now;

            var status = !isPast
                ? random.NextDouble() < 0.7 ? VisitStatuses.Scheduled : VisitStatuses.Confirmed
                : random.NextDouble() switch
                {
                    < 0.68 => VisitStatuses.Completed,
                    < 0.80 => VisitStatuses.NoShow,
                    < 0.90 => VisitStatuses.Cancelled,
                    _ => VisitStatuses.Rescheduled,
                };

            var completed = status == VisitStatuses.Completed;
            var interestRoll = random.NextDouble();
            var interest = !completed ? null
                : interestRoll < 0.32 ? InterestLevels.High
                : interestRoll < 0.72 ? InterestLevels.Medium
                : InterestLevels.Low;

            var checkIn = completed ? scheduledAt.AddMinutes(random.Next(-15, 40)) : (DateTime?)null;

            visits.Add(new SiteVisit
            {
                VisitCode = Controllers.CrmControllerBase.ReferenceCode("SV", scheduledAt),
                CompanyId = companyId,
                BranchId = lead?.BranchId ?? contact?.BranchId ?? branches[0].Id,
                LeadId = lead?.Id,
                ContactId = contact?.Id,
                ProjectId = project.Id,
                UnitId = unit?.Id,
                VisitorName = lead?.Name ?? contact?.FullName ?? "Visitor",
                VisitorPhone = lead?.Phone ?? contact?.Phone,
                PartySize = random.Next(1, 5),
                VisitType = random.NextDouble() < 0.62 ? VisitTypes.FirstVisit
                    : random.NextDouble() < 0.8 ? VisitTypes.RepeatVisit
                    : VisitTypes.ClosingVisit,
                Status = status,
                ScheduledAt = scheduledAt,
                DurationMinutes = new[] { 30, 45, 60, 90 }[random.Next(4)],
                CheckInAt = checkIn,
                CheckOutAt = checkIn?.AddMinutes(random.Next(35, 130)),
                HostId = host.Id,
                HostName = host.Name,
                TransportMode = random.NextDouble() < 0.4 ? "Company cab" : "Own vehicle",
                PickupLocation = random.NextDouble() < 0.4 ? Localities[random.Next(Localities.Length)] : null,
                // Feedback is labelled by interest level, which is what the
                // sentiment model trains against.
                Feedback = interest switch
                {
                    InterestLevels.High => Note(random, PositiveNotes),
                    InterestLevels.Low => Note(random, NegativeNotes),
                    InterestLevels.Medium => Note(random, NeutralNotes),
                    _ => null,
                },
                InterestLevel = interest,
                Rating = completed ? random.Next(2, 6) : null,
                BudgetDiscussed = completed
                    ? RoundTo((decimal)(random.Next(60, 300) * 100_000), 100_000m)
                    : null,
                NextAction = interest == InterestLevels.High ? "Share quotation and hold the unit" : null,
                CancellationReason = status == VisitStatuses.Cancelled ? "Visitor rescheduled at short notice" : null,
                CreatedAt = scheduledAt.AddDays(-random.Next(1, 8)),
                UpdatedAt = checkIn ?? scheduledAt,
            });
        }

        db.SiteVisits.AddRange(visits);
        await db.SaveChangesAsync();
    }

    /* ------------------------------------------------------------------ *
     * Calls
     * ------------------------------------------------------------------ */

    private static async Task SeedCallsAsync(
        AppDbContext db,
        int companyId,
        List<Branch> branches,
        List<User> users,
        List<Contact> contacts,
        Random random,
        DateTime now)
    {
        var leads = await db.Leads.Select(l => new { l.Id, l.Name, l.Phone, l.BranchId }).Take(400).ToListAsync();
        var calls = new List<CallLog>();

        for (var i = 0; i < 1100; i++)
        {
            var useLead = leads.Count > 0 && random.NextDouble() < 0.6;
            var lead = useLead ? leads[random.Next(leads.Count)] : null;
            var contact = useLead ? null : contacts[random.Next(contacts.Count)];
            var agent = users[random.Next(users.Count)];

            var startedAt = now.AddDays(-random.Next(0, 180)).Date
                .AddHours(random.Next(9, 20))
                .AddMinutes(random.Next(0, 60));

            var outcomeRoll = random.NextDouble();
            var outcome = outcomeRoll switch
            {
                < 0.62 => CallOutcomes.Connected,
                < 0.76 => CallOutcomes.NoAnswer,
                < 0.84 => CallOutcomes.Busy,
                < 0.91 => CallOutcomes.Voicemail,
                < 0.96 => CallOutcomes.SwitchedOff,
                _ => CallOutcomes.WrongNumber,
            };

            var connected = outcome == CallOutcomes.Connected;

            var dispositionRoll = random.NextDouble();
            var disposition = !connected ? null : dispositionRoll switch
            {
                < 0.30 => CallDispositions.Interested,
                < 0.48 => CallDispositions.CallBackLater,
                < 0.62 => CallDispositions.SiteVisitScheduled,
                < 0.76 => CallDispositions.NotInterested,
                < 0.86 => CallDispositions.BudgetMismatch,
                < 0.94 => CallDispositions.Converted,
                _ => CallDispositions.DoNotCall,
            };

            // The note matches the disposition — that pairing is the training
            // signal for the sentiment model.
            var notes = disposition switch
            {
                CallDispositions.Interested or CallDispositions.SiteVisitScheduled
                    or CallDispositions.Converted => Note(random, PositiveNotes),
                CallDispositions.NotInterested or CallDispositions.BudgetMismatch
                    or CallDispositions.DoNotCall => Note(random, NegativeNotes),
                CallDispositions.CallBackLater => Note(random, NeutralNotes),
                _ => null,
            };

            calls.Add(new CallLog
            {
                CompanyId = companyId,
                BranchId = lead?.BranchId ?? contact?.BranchId ?? branches[0].Id,
                RelatedType = useLead ? RelatedTypes.Lead : RelatedTypes.Contact,
                RelatedId = lead?.Id ?? contact!.Id,
                RelatedName = lead?.Name ?? contact!.FullName,
                Direction = random.NextDouble() < 0.72 ? CallDirections.Outbound
                    : random.NextDouble() < 0.8 ? CallDirections.Inbound
                    : CallDirections.Missed,
                Outcome = outcome,
                Disposition = disposition,
                PhoneNumber = lead?.Phone ?? contact?.Phone,
                StartedAt = startedAt,
                DurationSeconds = connected ? random.Next(45, 900) : random.Next(0, 25),
                WaitSeconds = random.Next(2, 30),
                AgentId = agent.Id,
                AgentName = agent.Name,
                Notes = notes,
                FollowUpAt = disposition == CallDispositions.CallBackLater
                    ? startedAt.AddDays(random.Next(1, 10))
                    : null,
                CreatedAt = startedAt,
                UpdatedAt = startedAt,
            });
        }

        db.CallLogs.AddRange(calls);
        await db.SaveChangesAsync();
    }

    /* ------------------------------------------------------------------ *
     * OBM visits
     * ------------------------------------------------------------------ */

    private static async Task SeedObmVisitsAsync(
        AppDbContext db,
        int companyId,
        List<Branch> branches,
        List<User> users,
        List<Lead> leads,
        Random random,
        DateTime now)
    {
        var visits = new List<ObmVisit>();

        for (var i = 0; i < 180; i++)
        {
            var agent = users[random.Next(users.Count)];
            var branch = branches[random.Next(branches.Count)];
            var scheduledAt = now.AddDays(random.Next(-150, 14)).Date.AddHours(random.Next(10, 18));
            var isPast = scheduledAt < now;

            var status = !isPast
                ? VisitStatuses.Scheduled
                : random.NextDouble() switch
                {
                    < 0.74 => VisitStatuses.Completed,
                    < 0.86 => VisitStatuses.Cancelled,
                    _ => VisitStatuses.NoShow,
                };

            var completed = status == VisitStatuses.Completed;
            var checkIn = completed ? scheduledAt.AddMinutes(random.Next(-10, 30)) : (DateTime?)null;
            var leadsGenerated = completed ? random.Next(0, 6) : 0;

            // Most OBMs are pure partner development; a minority are held on
            // behalf of one named lead, and only those show on the lead grid.
            var forLead = leads.Count > 0 && random.NextDouble() < 0.3
                ? leads[random.Next(leads.Count)]
                : null;

            visits.Add(new ObmVisit
            {
                VisitCode = Controllers.CrmControllerBase.ReferenceCode("OBM", scheduledAt),
                CompanyId = companyId,
                BranchId = branch.Id,
                LeadId = forLead?.Id,
                PartnerName = PartnerNames[random.Next(PartnerNames.Length)],
                PartnerType = PartnerTypes.All[random.Next(PartnerTypes.All.Length)],
                ContactPerson = $"{FirstNames[random.Next(FirstNames.Length)]} {LastNames[random.Next(LastNames.Length)]}",
                ContactPhone = $"98{random.Next(10000000, 99999999)}",
                Status = status,
                ScheduledAt = scheduledAt,
                DurationMinutes = new[] { 60, 90, 120 }[random.Next(3)],
                CheckInAt = checkIn,
                CheckOutAt = checkIn?.AddMinutes(random.Next(30, 120)),
                // Roughly greater-Mumbai coordinates, jittered per visit.
                Latitude = completed ? 19.0 + (random.NextDouble() * 0.25) : null,
                Longitude = completed ? 72.8 + (random.NextDouble() * 0.25) : null,
                LocationLabel = Localities[random.Next(Localities.Length)],
                City = branch.City,
                DistanceKm = completed ? decimal.Round((decimal)(random.NextDouble() * 45), 1) : null,
                ExpenseAmount = completed ? decimal.Round((decimal)random.Next(150, 2200), 0) : null,
                Purpose = new[]
                {
                    "Quarterly inventory briefing",
                    "New launch presentation",
                    "Commission structure review",
                    "Joint site walkthrough",
                    "Corporate tie-up discussion",
                    "Loan partner alignment",
                }[random.Next(6)],
                Outcome = completed
                    ? leadsGenerated > 2 ? "Strong response — partner committed to a joint campaign."
                        : leadsGenerated > 0 ? "Positive meeting, a few referrals expected."
                        : "Introductory meeting, no immediate business."
                    : null,
                MeetingNotes = completed ? Note(random, NeutralNotes) : null,
                LeadsGenerated = leadsGenerated,
                BusinessValue = leadsGenerated > 0
                    ? RoundTo((decimal)(leadsGenerated * random.Next(50, 180) * 100_000), 100_000m)
                    : null,
                NextMeetingAt = completed && random.NextDouble() < 0.5
                    ? scheduledAt.AddDays(random.Next(14, 90))
                    : null,
                AgentId = agent.Id,
                AgentName = agent.Name,
                CreatedAt = scheduledAt.AddDays(-random.Next(1, 10)),
                UpdatedAt = checkIn ?? scheduledAt,
            });
        }

        db.ObmVisits.AddRange(visits);
        await db.SaveChangesAsync();
    }

    /* ------------------------------------------------------------------ *
     * Follow-ups
     * ------------------------------------------------------------------ */

    private static async Task SeedFollowUpsAsync(
        AppDbContext db,
        int companyId,
        List<Branch> branches,
        List<User> users,
        List<Contact> contacts,
        List<Opportunity> opportunities,
        Random random,
        DateTime now)
    {
        var leads = await db.Leads.Select(l => new { l.Id, l.Name, l.BranchId }).Take(400).ToListAsync();
        var followUps = new List<FollowUp>();

        var subjects = new[]
        {
            "Call back to confirm site visit",
            "Share revised quotation",
            "Send floor plans on WhatsApp",
            "Follow up on loan documentation",
            "Confirm token payment",
            "Reconfirm weekend visit",
            "Discuss payment schedule",
            "Send agreement draft",
            "Check on family decision",
            "Arrange a repeat visit for the spouse",
        };

        for (var i = 0; i < 420; i++)
        {
            var roll = random.NextDouble();
            var owner = users[random.Next(users.Count)];

            string relatedType;
            int relatedId;
            string relatedName;
            int branchId;

            if (roll < 0.5 && leads.Count > 0)
            {
                var lead = leads[random.Next(leads.Count)];
                (relatedType, relatedId, relatedName, branchId) =
                    (RelatedTypes.Lead, lead.Id, lead.Name, lead.BranchId);
            }
            else if (roll < 0.8)
            {
                var opportunity = opportunities[random.Next(opportunities.Count)];
                (relatedType, relatedId, relatedName, branchId) =
                    (RelatedTypes.Opportunity, opportunity.Id, opportunity.Name, opportunity.BranchId);
            }
            else
            {
                var contact = contacts[random.Next(contacts.Count)];
                (relatedType, relatedId, relatedName, branchId) =
                    (RelatedTypes.Contact, contact.Id, contact.FullName, contact.BranchId);
            }

            // Deliberately spread across the past and future so the agenda strip
            // has genuine overdue, today and upcoming buckets rather than one
            // flat pile.
            var dueAt = now.AddHours(random.Next(-720, 336));
            var isPast = dueAt < now;

            var status = isPast
                ? random.NextDouble() switch
                {
                    < 0.62 => FollowUpStatuses.Completed,
                    < 0.86 => FollowUpStatuses.Open,
                    < 0.94 => FollowUpStatuses.InProgress,
                    _ => FollowUpStatuses.Cancelled,
                }
                : random.NextDouble() < 0.85 ? FollowUpStatuses.Open : FollowUpStatuses.InProgress;

            var createdAt = dueAt.AddDays(-random.Next(1, 12));

            followUps.Add(new FollowUp
            {
                CompanyId = companyId,
                BranchId = branchId,
                Subject = subjects[random.Next(subjects.Length)],
                Description = random.NextDouble() < 0.4 ? Note(random, NeutralNotes) : null,
                RelatedType = relatedType,
                RelatedId = relatedId,
                RelatedName = relatedName,
                Channel = FollowUpChannels.All[random.Next(FollowUpChannels.All.Length)],
                Status = status,
                Priority = LeadPriorities.All[random.Next(LeadPriorities.All.Length)],
                DueAt = dueAt,
                ReminderAt = dueAt.AddHours(-random.Next(1, 6)),
                CompletedAt = status == FollowUpStatuses.Completed
                    ? dueAt.AddHours(random.Next(-6, 30))
                    : null,
                Outcome = status == FollowUpStatuses.Completed
                    ? Note(random, NeutralNotes)
                    : null,
                SlaMinutes = new[] { 240, 480, 1440, 2880 }[random.Next(4)],
                OwnerId = owner.Id,
                CreatedAt = createdAt > now ? now : createdAt,
                UpdatedAt = isPast ? dueAt : createdAt,
            });
        }

        db.FollowUps.AddRange(followUps);
        await db.SaveChangesAsync();
    }
}
