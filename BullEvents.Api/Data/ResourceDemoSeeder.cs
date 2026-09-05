using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Data;

/// <summary>
/// A small starting set for the vendor, crew and fleet modules — enough that
/// the screens open on something recognisable instead of four empty tables.
///
/// Same contract as <see cref="EventDemoSeeder"/>: additive, and it refuses to
/// run the moment a vendor exists, so it can never land on a tenant that has
/// begun entering its own suppliers.
///
/// The kits are built from the imported décor register rather than invented, so
/// a mandap kit points at real stock with real photographs. Where the register
/// does not hold a match the line is skipped rather than faked.
/// </summary>
public static class ResourceDemoSeeder
{
    private record VendorSeed(
        string Name,
        string[] Services,
        string Contact,
        string Phone,
        string City,
        int Capacity,
        decimal Rating,
        (string Service, string Name, string Basis, decimal Rate, decimal Sell)[] Rates);

    private static readonly VendorSeed[] Vendors =
    [
        new("Annapurna Caterers", [ServiceCategories.Catering], "Suresh Iyer",
            "9820100101", "Mumbai", 3, 4.6m,
            [
                (ServiceCategories.Catering, "Veg thali — silver menu", VendorRateBases.PerPlate, 850m, 1200m),
                (ServiceCategories.Catering, "Veg thali — gold menu", VendorRateBases.PerPlate, 1250m, 1800m),
                (ServiceCategories.Catering, "Live chaat counter", VendorRateBases.PerEvent, 18000m, 28000m),
            ]),

        new("Dhruv Sound & Light", [ServiceCategories.SoundAndLight, ServiceCategories.Entertainment],
            "Dhruv Mehta", "9820100102", "Mumbai", 2, 4.4m,
            [
                (ServiceCategories.SoundAndLight, "Sangeet PA + lighting rig", VendorRateBases.PerEvent, 45000m, 70000m),
                (ServiceCategories.Entertainment, "DJ, 4 hours", VendorRateBases.PerEvent, 25000m, 40000m),
            ]),

        new("Lenscraft Studios", [ServiceCategories.Photography, ServiceCategories.Videography],
            "Ritu Bhandari", "9820100103", "Mumbai", 1, 4.8m,
            [
                (ServiceCategories.Photography, "Candid photography, full day", VendorRateBases.PerDay, 35000m, 55000m),
                (ServiceCategories.Videography, "Cinematic film, full day", VendorRateBases.PerDay, 45000m, 72000m),
            ]),

        new("Phoolwala Florists", [ServiceCategories.Decor], "Imran Shaikh",
            "9820100104", "Mumbai", 4, 4.2m,
            [
                (ServiceCategories.Decor, "Fresh marigold garland", VendorRateBases.PerPiece, 180m, 320m),
                (ServiceCategories.Decor, "Stage floral backdrop", VendorRateBases.PerEvent, 40000m, 65000m),
            ]),

        new("Sharma Tent & Mandap", [ServiceCategories.Decor], "Rakesh Sharma",
            "9820100105", "Mumbai", 2, 3.9m,
            [
                (ServiceCategories.Decor, "Traditional mandap, built", VendorRateBases.PerEvent, 65000m, 105000m),
                (ServiceCategories.Decor, "Shamiana, per sq ft", VendorRateBases.PerSqft, 45m, 80m),
            ]),

        new("Glam Room Makeovers", [ServiceCategories.Makeup, ServiceCategories.Mehendi],
            "Neha Kulkarni", "9820100106", "Mumbai", 1, 4.7m,
            [
                (ServiceCategories.Makeup, "Bridal makeup + draping", VendorRateBases.PerEvent, 28000m, 45000m),
                (ServiceCategories.Mehendi, "Bridal mehendi", VendorRateBases.PerEvent, 12000m, 20000m),
            ]),

        new("Ganesh Transport", [ServiceCategories.Transport], "Ganesh Patil",
            "9820100107", "Mumbai", 5, 4.0m,
            [
                (ServiceCategories.Transport, "Tempo, per day", VendorRateBases.PerDay, 4500m, 7000m),
                (ServiceCategories.Transport, "35-seat coach, per day", VendorRateBases.PerDay, 12000m, 18000m),
            ]),

        new("Shubh Vivah Pandits", [ServiceCategories.Priest], "Pandit Ramesh Joshi",
            "9820100108", "Mumbai", 2, 4.9m,
            [
                (ServiceCategories.Priest, "Vivah ceremony", VendorRateBases.PerEvent, 21000m, 31000m),
            ]),
    ];

    private record CrewSeed(
        string Name,
        string Role,
        string[] AlsoCovers,
        string Engagement,
        string Phone,
        decimal DayRate,
        int Experience,
        decimal Rating,
        bool Travels);

    private static readonly CrewSeed[] Crew =
    [
        new("Mahesh Gaikwad", CrewRoles.Supervisor, [CrewRoles.Decorator], CrewEngagementTypes.Employee, "9820200101", 2500m, 11, 4.7m, true),
        new("Sunil Rathod", CrewRoles.Decorator, [CrewRoles.Carpenter], CrewEngagementTypes.Freelancer, "9820200102", 1800m, 8, 4.5m, true),
        new("Prakash More", CrewRoles.Carpenter, [CrewRoles.Helper], CrewEngagementTypes.Freelancer, "9820200103", 1600m, 12, 4.3m, false),
        new("Ashok Pawar", CrewRoles.Electrician, [CrewRoles.LightTechnician], CrewEngagementTypes.Freelancer, "9820200104", 2000m, 9, 4.6m, true),
        new("Vikas Jadhav", CrewRoles.LightTechnician, [CrewRoles.SoundTechnician], CrewEngagementTypes.Freelancer, "9820200105", 2200m, 6, 4.1m, true),
        new("Ramesh Shinde", CrewRoles.Loader, [CrewRoles.Helper], CrewEngagementTypes.Contractor, "9820200106", 900m, 4, 3.8m, false),
        new("Santosh Kamble", CrewRoles.Loader, [CrewRoles.Helper], CrewEngagementTypes.Contractor, "9820200107", 900m, 3, 4.0m, false),
        new("Dinesh Sawant", CrewRoles.Bearer, [CrewRoles.Usher], CrewEngagementTypes.Freelancer, "9820200108", 1100m, 5, 4.4m, false),
        new("Nitin Bhosale", CrewRoles.Bearer, [], CrewEngagementTypes.Freelancer, "9820200109", 1100m, 2, 3.9m, false),
        new("Kiran Salunkhe", CrewRoles.Bearer, [CrewRoles.Housekeeping], CrewEngagementTypes.Freelancer, "9820200110", 1100m, 6, 4.5m, false),
        new("Ravi Chavan", CrewRoles.Bearer, [], CrewEngagementTypes.Freelancer, "9820200111", 1100m, 3, 4.2m, false),
        new("Sanjay Deshmukh", CrewRoles.Driver, [CrewRoles.Loader], CrewEngagementTypes.Employee, "9820200112", 1400m, 15, 4.8m, true),
        new("Arun Naik", CrewRoles.Driver, [], CrewEngagementTypes.Employee, "9820200113", 1400m, 7, 4.3m, true),
        new("Pooja Ranade", CrewRoles.Coordinator, [CrewRoles.EventManager], CrewEngagementTypes.Employee, "9820200114", 3000m, 5, 4.6m, true),
        new("Shalini Nadkarni", CrewRoles.EventManager, [CrewRoles.Coordinator], CrewEngagementTypes.Employee, "9820200115", 4000m, 10, 4.9m, true),
        new("Farhan Qureshi", CrewRoles.Florist, [CrewRoles.Decorator], CrewEngagementTypes.Freelancer, "9820200116", 1700m, 8, 4.4m, false),
        new("Manoj Tandel", CrewRoles.Security, [], CrewEngagementTypes.Contractor, "9820200117", 1000m, 6, 4.0m, false),
        new("Balu Wagh", CrewRoles.Helper, [CrewRoles.Loader], CrewEngagementTypes.Contractor, "9820200118", 800m, 2, 3.7m, false),
    ];

    private record VehicleSeed(
        string Registration,
        string Name,
        string Type,
        decimal Payload,
        decimal Capacity,
        int? Seats,
        decimal DayRate);

    private static readonly VehicleSeed[] Fleet =
    [
        new("MH 01 AB 1234", "Tempo 1", VehicleTypes.Tempo, 1500m, 350m, 2, 4500m),
        new("MH 01 AB 5678", "Tempo 2", VehicleTypes.Tempo, 1500m, 350m, 2, 4500m),
        new("MH 02 CD 9012", "Eicher 14ft", VehicleTypes.Truck, 4000m, 900m, 2, 8500m),
        new("MH 02 CD 3456", "Container 20ft", VehicleTypes.Container, 7000m, 1600m, 2, 14000m),
        new("MH 03 EF 7890", "Pickup", VehicleTypes.Pickup, 800m, 180m, 2, 2800m),
    ];

    /// <summary>
    /// Kits, expressed as searches against the imported register rather than as
    /// item ids — the ids differ per tenant, the names do not.
    /// </summary>
    private record KitSeed(
        string Name,
        string Code,
        string SetupType,
        string? EventType,
        decimal SetupHours,
        int Crew,
        (string Contains, int Quantity, bool Optional)[] Lines);

    private static readonly KitSeed[] Kits =
    [
        new("Traditional Mandap Set", "KIT-MANDAP", "Mandap", EventTypes.Wedding, 6m, 6,
            [
                ("BIG FIBRE POT", 4, false),
                ("BRASS URLI", 2, false),
                ("KALIN", 4, false),
                ("CRYSTAL CHANDELIERS", 2, true),
                ("MORACCAN LAMP", 8, true),
                ("GOLDEN VASE", 6, true),
            ]),

        new("Sangeet Stage Set", "KIT-SANGEET", "Stage", EventTypes.Sangeet, 4m, 4,
            [
                ("CRYSTAL CHANDELIERS", 3, false),
                ("BLACK CANDLE STAND", 12, false),
                ("MIRROR", 2, true),
                ("HEXAGON HANGINGS", 6, true),
            ]),

        new("Entrance Arch Set", "KIT-ENTRANCE", "Entrance", null, 3m, 3,
            [
                ("FIBRE POT GOLDEN", 4, false),
                ("ELECTRIC UMBRELLA", 4, false),
                ("MORACCAN LAMP", 6, true),
            ]),

        new("Round Table Centrepiece", "KIT-TABLE", "TableSet", null, 1m, 2,
            [
                ("ROUND TABLE CENTRAL PIECES", 1, false),
                ("GOLDEN FISH BOWL", 1, false),
                ("CANDLE", 3, true),
            ]),
    ];

    public static async Task SeedAsync(AppDbContext db)
    {
        // One vendor anywhere means these modules are in use. Never add to them.
        if (await db.Vendors.IgnoreQueryFilters().AnyAsync()) return;

        var company = await db.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync();
        if (company is null) return;

        var owner = await db.Users
            .Where(u => u.CompanyId == company.Id && u.Role != Roles.SuperAdmin)
            .OrderBy(u => u.Id)
            .FirstOrDefaultAsync();

        /* ---------------- vendors ---------------- */

        var vendors = new List<Vendor>();
        var index = 1;

        foreach (var seed in Vendors)
        {
            var vendor = new Vendor
            {
                CompanyId = company.Id,
                Name = seed.Name,
                Code = $"VN-{index++:D4}",
                Status = VendorStatuses.Active,
                Services = string.Join(",", seed.Services),
                ContactPerson = seed.Contact,
                Phone = seed.Phone,
                City = seed.City,
                CoverageAreas = "Mumbai,Pune,Nashik",
                ConcurrentEventCapacity = seed.Capacity,
                Rating = seed.Rating,
                PaymentTermDays = 21,
                AdvanceFraction = 0.4m,
                OwnerId = owner?.Id,
            };

            foreach (var rate in seed.Rates)
            {
                vendor.Rates.Add(new VendorRate
                {
                    CompanyId = company.Id,
                    Service = rate.Service,
                    Name = rate.Name,
                    Basis = rate.Basis,
                    Rate = rate.Rate,
                    SellRate = rate.Sell,
                });
            }

            vendors.Add(vendor);
        }

        db.Vendors.AddRange(vendors);
        await db.SaveChangesAsync();

        /* ---------------- crew ---------------- */

        var transporter = vendors.FirstOrDefault(v => v.Name == "Ganesh Transport");
        var crew = new List<CrewMember>();
        index = 1;

        foreach (var seed in Crew)
        {
            crew.Add(new CrewMember
            {
                CompanyId = company.Id,
                Name = seed.Name,
                Code = $"CR-{index++:D4}",
                EngagementType = seed.Engagement,
                Status = CrewStatuses.Active,
                PrimaryRole = seed.Role,
                SecondaryRoles = seed.AlsoCovers.Length == 0
                    ? null
                    : string.Join(",", seed.AlsoCovers),
                Phone = seed.Phone,
                City = "Mumbai",
                WillTravel = seed.Travels,
                DayRate = seed.DayRate,
                OvertimeHourlyRate = Math.Round(seed.DayRate / 8m, 0),
                YearsExperience = seed.Experience,
                Rating = seed.Rating,
                // A contractor's hands come through the labour supplier, which
                // is what the link records — the gang is billed to them, not
                // paid to the person.
                SupplierVendorId = seed.Engagement == CrewEngagementTypes.Contractor
                    ? transporter?.Id
                    : null,
                OwnerId = owner?.Id,
            });
        }

        db.CrewMembers.AddRange(crew);
        await db.SaveChangesAsync();

        /* ---------------- fleet ---------------- */

        var godown = await db.PropStores.OrderBy(s => s.Id).FirstOrDefaultAsync();
        var drivers = crew.Where(c => c.PrimaryRole == CrewRoles.Driver).ToList();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var vehicles = Fleet.Select((seed, i) => new Vehicle
        {
            CompanyId = company.Id,
            RegistrationNumber = seed.Registration,
            Name = seed.Name,
            VehicleType = seed.Type,
            Status = VehicleStatuses.Active,
            PayloadKg = seed.Payload,
            CapacityCubicFeet = seed.Capacity,
            PassengerSeats = seed.Seats,
            DayRate = seed.DayRate,
            RatePerKm = 24m,
            DefaultDriverCrewId = drivers.Count == 0 ? null : drivers[i % drivers.Count].Id,
            StoreId = godown?.Id,
            // Staggered so the compliance screen has something to show without
            // every vehicle being off the road at once.
            InsuranceExpiry = today.AddMonths(3 + i),
            PermitExpiry = today.AddMonths(6 + i),
            PucExpiry = today.AddMonths(1 + i),
            FitnessExpiry = today.AddMonths(9 + i),
            LastServicedOn = today.AddDays(-30 * (i + 1)),
            OdometerKm = 40_000 + (i * 12_500),
        }).ToList();

        db.Vehicles.AddRange(vehicles);
        await db.SaveChangesAsync();

        /* ---------------- kits, built from the real register ---------------- */

        var items = await db.PropItems
            .Where(i => i.Status == PropItemStatuses.Active && i.GoodQuantity > 0)
            .Select(i => new { i.Id, i.Name, i.GoodQuantity })
            .ToListAsync();

        if (items.Count == 0) return;

        var kits = new List<PropKit>();

        foreach (var seed in Kits)
        {
            var kit = new PropKit
            {
                CompanyId = company.Id,
                Name = seed.Name,
                Code = seed.Code,
                SetupType = seed.SetupType,
                EventType = seed.EventType,
                SetupHours = seed.SetupHours,
                CrewRequired = seed.Crew,
                Description = $"Standard {seed.SetupType.ToLowerInvariant()} set, built from the décor register.",
            };

            var order = 0;
            var used = new HashSet<int>();

            foreach (var (contains, quantity, optional) in seed.Lines)
            {
                // Best match by stock on hand, so a kit points at the line the
                // godown can actually field rather than the first alphabetical
                // near-miss.
                var match = items
                    .Where(i => i.Name.Contains(contains, StringComparison.OrdinalIgnoreCase))
                    .Where(i => !used.Contains(i.Id))
                    .OrderByDescending(i => i.GoodQuantity)
                    .FirstOrDefault();

                if (match is null || match.GoodQuantity < quantity) continue;

                used.Add(match.Id);
                kit.Lines.Add(new PropKitLine
                {
                    PropItemId = match.Id,
                    Quantity = quantity,
                    IsOptional = optional,
                    SortOrder = order++,
                });
            }

            if (kit.Lines.Count > 0) kits.Add(kit);
        }

        if (kits.Count > 0)
        {
            db.PropKits.AddRange(kits);
            await db.SaveChangesAsync();
        }
    }
}
