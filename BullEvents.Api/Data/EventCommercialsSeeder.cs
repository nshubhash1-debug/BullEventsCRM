using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Data;

/// <summary>
/// The payment plans and service lines an events desk quotes from on day one.
///
/// Seeded for the same reason the letter templates are: an empty list is a
/// feature nobody switches on. Nobody sets up a CRM by inventing a payment
/// schedule from nothing — they want to see the terms they already work on,
/// change the two percentages that differ, and get on with quoting.
///
/// The schedules here are anchored to the <b>event date</b>, not the booking
/// date. That is the difference between an events plan and a property one: a
/// flat is paid off a booking because possession is years out and moves, while
/// an event is paid off the date itself, which is fixed before the contract is
/// signed and is the only date the client is thinking about.
///
/// Additive only. A plan somebody renamed is never overwritten, and one they
/// retired is not resurrected.
/// </summary>
public static class EventCommercialsSeeder
{
    private record MilestoneSeed(
        string Label,
        string Basis,
        decimal Percent,
        int? OffsetDays,
        string Anchor);

    private record PlanSeed(
        string Code,
        string Name,
        string Description,
        decimal StandardDiscount,
        decimal DiscountTolerance,
        MilestoneSeed[] Milestones);

    /// <summary>
    /// Three ways an event gets paid for.
    ///
    /// All three end at the event rather than after it, because the one rule
    /// every venue and planner shares is that the balance clears before the
    /// guests arrive — an unpaid client on the morning of a wedding has all the
    /// leverage, and the industry prices that risk out rather than chasing it.
    /// The settlement line that follows is only ever for guests above the
    /// contracted count.
    /// </summary>
    private static readonly PlanSeed[] Plans =
    [
        new("EVT-STD", "Standard — 25 / 50 / 25",
            "Booking advance, half a month out, balance a week before. The default terms.",
            StandardDiscount: 0m, DiscountTolerance: 0.05m,
            [
                new("Booking advance — 25%", MilestoneBases.PercentOfTotal, 0.25m,
                    0, MilestoneAnchors.FromBooking),
                new("Second instalment — 50%", MilestoneBases.PercentOfTotal, 0.50m,
                    30, MilestoneAnchors.BeforeEvent),
                new("Balance before the event — 25%", MilestoneBases.BalanceToPercent, 1.00m,
                    7, MilestoneAnchors.BeforeEvent),
                new("Settlement of actuals", MilestoneBases.Fixed, 0m,
                    7, MilestoneAnchors.AfterEvent),
            ]),

        new("EVT-UPFRONT", "Paid up front",
            "The whole amount on booking. Earns the best rate because it carries no collection risk.",
            StandardDiscount: 0.10m, DiscountTolerance: 0.05m,
            [
                new("Full payment on booking — 100%", MilestoneBases.PercentOfTotal, 1.00m,
                    0, MilestoneAnchors.FromBooking),
                new("Settlement of actuals", MilestoneBases.Fixed, 0m,
                    7, MilestoneAnchors.AfterEvent),
            ]),

        new("EVT-CORP", "Corporate — 50 / 50 on PO",
            "Half against the purchase order, the balance on completion. For company accounts billing to a PO.",
            StandardDiscount: 0.05m, DiscountTolerance: 0.05m,
            [
                new("On purchase order — 50%", MilestoneBases.PercentOfTotal, 0.50m,
                    0, MilestoneAnchors.FromBooking),
                new("Balance on completion — 50%", MilestoneBases.BalanceToPercent, 1.00m,
                    15, MilestoneAnchors.AfterEvent),
            ]),
    ];

    /// <summary>
    /// The lines almost every proposal carries.
    ///
    /// Catering is <see cref="ChargeBases.PerGuest"/> and mandatory: it is the
    /// largest number on the bill and the one a rep is most likely to leave off
    /// a draft and then have to reissue.
    /// </summary>
    private record ChargeSeed(
        string Group,
        string Code,
        string Name,
        string Description,
        string Basis,
        decimal Rate,
        decimal TaxRate,
        decimal DefaultQuantity,
        bool IsMandatory,
        bool IsRefundable,
        bool IncludeInSchedule,
        string? DueLabel,
        int SortOrder);

    private static readonly ChargeSeed[] Charges =
    [
        new(ChargeGroups.Catering, "EVT-CATER", "Catering",
            "Per head, billed on the higher of the guest count and the venue's minimum guarantee.",
            ChargeBases.PerGuest, 1_800m, 0.05m, 1m, true, false, true, null, 10),

        new(ChargeGroups.Catering, "EVT-BAR", "Bar service",
            "Bar setup, staffing and service. Beverages billed on consumption.",
            ChargeBases.PerGuest, 450m, 0.18m, 1m, false, false, true, null, 20),

        new(ChargeGroups.Decor, "EVT-DECOR", "Décor & floral",
            "Stage, entrance, table and mandap styling.",
            ChargeBases.Lumpsum, 250_000m, 0.18m, 1m, false, false, true, null, 30),

        new(ChargeGroups.Decor, "EVT-DRAPE", "Draping & rigging",
            "Ceiling and wall treatment, charged on the area covered.",
            ChargeBases.PerSqft, 45m, 0.18m, 1m, false, false, true, null, 40),

        new(ChargeGroups.Photography, "EVT-PHOTO", "Photography & film",
            "Crew for the day, edited album and film.",
            ChargeBases.PerQuantity, 60_000m, 0.18m, 2m, false, false, true, null, 50),

        new(ChargeGroups.Entertainment, "EVT-DJ", "DJ & sound",
            "DJ, console and floor sound for the evening.",
            ChargeBases.Lumpsum, 85_000m, 0.18m, 1m, false, false, true, null, 60),

        new(ChargeGroups.Entertainment, "EVT-LIGHT", "Stage & ambient lighting",
            "Rig, control and an operator on site.",
            ChargeBases.Lumpsum, 120_000m, 0.18m, 1m, false, false, true, null, 70),

        new(ChargeGroups.Logistics, "EVT-CREW", "Event crew",
            "Coordinators, ushers and helpers on the day.",
            ChargeBases.PerQuantity, 2_500m, 0.18m, 8m, false, false, true, null, 80),

        new(ChargeGroups.Logistics, "EVT-VALET", "Valet & parking management",
            "Valet crew and traffic marshalling.",
            ChargeBases.Lumpsum, 35_000m, 0.18m, 1m, false, false, true, null, 90),

        new(ChargeGroups.Logistics, "EVT-PLAN", "Planning & coordination fee",
            "The planner's own fee, as a share of the venue rental.",
            ChargeBases.PercentOfUnitCost, 0.10m, 0.18m, 1m, false, false, true, null, 100),

        new(ChargeGroups.Statutory, "EVT-PERMIT", "Permits & licences",
            "Music licence, late-hours permission and fire clearance.",
            ChargeBases.Lumpsum, 25_000m, 0.18m, 1m, false, false, true, null, 110),

        // Outside the schedule: the deposit is taken on booking and returned
        // after the event, so spreading it across the instalments would misstate
        // every one of them and then have to be unwound at refund.
        new(ChargeGroups.Deposits, "EVT-SEC", "Refundable security deposit",
            "Held against damage and overrun. Returned within 15 days of the event.",
            ChargeBases.Lumpsum, 100_000m, 0m, 1m, false, true, false,
            "Payable on booking, refunded after the event", 120),
    ];

    public static async Task SeedAsync(AppDbContext db, int companyId)
    {
        await SeedPlansAsync(db, companyId);
        await SeedChargesAsync(db, companyId);
    }

    private static async Task SeedPlansAsync(AppDbContext db, int companyId)
    {
        // Matched on code rather than name: a plan's name is the thing a desk
        // rewords ("Standard" becomes "Regular terms"), while the code is what
        // they leave alone. Keying on the name would re-create the plan the
        // moment somebody renamed it.
        var have = await db.PaymentPlans
            .IgnoreQueryFilters()
            .Where(p => p.CompanyId == companyId)
            .Select(p => p.Code)
            .ToListAsync();

        var known = new HashSet<string>(have, StringComparer.OrdinalIgnoreCase);
        var sortOrder = 0;

        foreach (var seed in Plans)
        {
            sortOrder++;
            if (known.Contains(seed.Code)) continue;

            var plan = new PaymentPlan
            {
                CompanyId = companyId,
                Code = seed.Code,
                Name = seed.Name,
                Description = seed.Description,
                StandardDiscount = seed.StandardDiscount,
                DiscountTolerance = seed.DiscountTolerance,

                // Events are a service, taxed at 18% — not the 12% a
                // under-construction flat carries.
                TaxRate = 0.18m,
                SortOrder = sortOrder,
            };

            var order = 0;
            foreach (var milestone in seed.Milestones)
            {
                plan.Milestones.Add(new PaymentPlanMilestone
                {
                    SortOrder = order++,
                    Label = milestone.Label,
                    Basis = milestone.Basis,
                    Percent = milestone.Percent,
                    DueOffsetDays = milestone.OffsetDays,
                    DueAnchor = milestone.Anchor,
                });
            }

            db.PaymentPlans.Add(plan);
        }

        await db.SaveChangesAsync();
    }

    private static async Task SeedChargesAsync(AppDbContext db, int companyId)
    {
        var have = await db.ChargeHeads
            .IgnoreQueryFilters()
            .Where(c => c.CompanyId == companyId)
            .Select(c => c.Code)
            .ToListAsync();

        var known = new HashSet<string>(have, StringComparer.OrdinalIgnoreCase);

        foreach (var seed in Charges)
        {
            if (known.Contains(seed.Code)) continue;

            db.ChargeHeads.Add(new ChargeHead
            {
                CompanyId = companyId,

                // Null rather than a venue id: these are the planner's own
                // service lines and apply wherever the event is held.
                ProjectId = null,

                Group = seed.Group,
                Code = seed.Code,
                Name = seed.Name,
                Description = seed.Description,
                Basis = seed.Basis,
                Rate = seed.Rate,
                TaxRate = seed.TaxRate,
                DefaultQuantity = seed.DefaultQuantity,
                IsMandatory = seed.IsMandatory,
                IsRefundable = seed.IsRefundable,
                IncludeInSchedule = seed.IncludeInSchedule,
                DueLabel = seed.DueLabel,
                SortOrder = seed.SortOrder,
            });
        }

        await db.SaveChangesAsync();
    }
}
