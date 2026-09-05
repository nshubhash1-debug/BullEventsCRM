using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Data;

/// <summary>
/// Gives every company its own copy of the lists the product ships with.
///
/// A copy per tenant rather than one shared table, because the whole point is
/// that a company can rename "Walk-in" to "Showroom footfall" without doing it
/// for everybody else. The values are what the seeders and the demo data
/// already write, so an existing database keeps reading correctly.
///
/// Idempotent, and it never overwrites: once an administrator has renamed a
/// value it is theirs, and a redeploy that reset their vocabulary would be a
/// small betrayal repeated on every release.
/// </summary>
public static class PickListSeeder
{
    private static readonly (string List, string[] Values)[] Shipped =
    [
        (PickLists.LeadSource, [
            "Website", "Walk-in", "Referral", "Portal", "Campaign",
            "Channel Partner", "Cold Call", "Social Media", "Other",
        ]),

        (PickLists.LossReason, [
            "Date unavailable", "Budget mismatch", "Went with another planner",
            "Went with another venue", "Event postponed", "Too far out — nurture",
            "Unresponsive", "Out of service area",
            "Budget", "Location", "Timing", "Went with a competitor",
            "Loan declined", "Duplicate enquiry", "Not serious",
        ]),

        (PickLists.RequirementType, [
            "Apartment", "Villa", "Plot", "Commercial", "Office", "Retail", "Warehouse",
        ]),

        (PickLists.FundingMode, [
            "Self-funded", "Home loan", "Part loan", "Company funded", "Undecided",
        ]),

        (PickLists.CallOutcome, [
            "Connected", "No answer", "Busy", "Switched off",
            "Wrong number", "Call back later", "Not interested",
        ]),

        (PickLists.VisitOutcome, [
            "Interested", "Needs another visit", "Booked",
            "Not interested", "Did not turn up", "Rescheduled",
            "Chose another date",
        ]),
    ];

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        var companies = await db.Companies.IgnoreQueryFilters().Select(c => c.Id).ToListAsync(ct);
        if (companies.Count == 0) return;

        foreach (var companyId in companies)
        {
            var existing = await db.PickListValues
                .IgnoreQueryFilters()
                .Where(v => v.CompanyId == companyId)
                .Select(v => new { v.List, v.Value })
                .ToListAsync(ct);

            var have = existing
                .Select(v => $"{v.List}::{v.Value}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var (list, values) in Shipped)
            {
                for (var index = 0; index < values.Length; index++)
                {
                    var value = values[index];
                    if (have.Contains($"{list}::{value}")) continue;

                    db.PickListValues.Add(new PickListValue
                    {
                        CompanyId = companyId,
                        List = list,
                        Value = value,
                        Label = value,
                        SortOrder = index,
                        IsSystem = true,
                    });
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
