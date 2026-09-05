using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Data;

/// <summary>
/// The job titles a developer's sales and post-sales floor actually uses.
///
/// Seeded rather than left blank for the same reason the letter templates are:
/// an empty list is a feature nobody switches on. Nobody sets up a CRM by
/// inventing an org chart from nothing on day one — they want to see their own
/// floor reflected back, rename the two titles that differ, and get on with it.
///
/// The levels are the grades this industry runs on, top down. Several titles
/// share a level on purpose: an AVP for sales and an AVP for post-sales are
/// peers, and forcing them into a strict order invents a hierarchy that does
/// not exist.
///
/// Additive only. A title somebody renamed is never overwritten, and one they
/// retired is not resurrected.
/// </summary>
public static class DesignationSeeder
{
    private record Seed(string Name, string Code, int Level, string? Role, string Description);

    private static readonly Seed[] Defaults =
    [
        new("Managing Director", "MD", 1, Roles.CompanyAdmin,
            "Owns the company. Sees everything."),

        new("Chief Executive Officer", "CEO", 1, Roles.CompanyAdmin,
            "Runs the business day to day."),

        new("Vice President — Sales", "VP-S", 2, Roles.Agm,
            "Owns the revenue number across every site."),

        new("Vice President — Operations", "VP-O", 2, Roles.Agm,
            "Owns delivery: construction, post-sales and customer care."),

        new("Assistant General Manager", "AGM", 3, Roles.Agm,
            "Runs a branch or a region, with several teams under them."),

        new("General Manager — Sales", "GM-S", 3, Roles.Agm,
            "Owns a project's sales from launch to sell-out."),

        new("Sales Manager", "SM", 4, Roles.SalesManager,
            "Runs a sales team and carries its target."),

        new("Post-Sales Manager", "PSM", 4, Roles.PostSales,
            "Runs collections, documentation and handover for a project."),

        new("Channel Partner Manager", "CPM", 4, Roles.SalesManager,
            "Owns the broker network and what it brings in."),

        new("Senior Relationship Manager", "SRM", 5, Roles.SalesExecutive,
            "Closes on their own book, and mentors the desk."),

        new("Relationship Manager", "RM", 6, Roles.SalesExecutive,
            "Works site visits and closes bookings."),

        new("Sales Executive", "SE", 7, Roles.SalesExecutive,
            "Handles enquiries and site visits."),

        new("Tele-calling Executive", "TCE", 7, Roles.TeleSales,
            "Qualifies inbound and calls the database."),

        new("Customer Relationship Executive", "CRE", 6, Roles.PostSales,
            "The buyer's point of contact after booking."),

        new("Collections Executive", "CE", 6, Roles.PostSales,
            "Chases demands and reconciles receipts."),

        new("Documentation Executive", "DE", 6, Roles.BackOffice,
            "Drafts agreements and runs registration."),

        new("Accounts Manager", "AM", 4, Roles.BackOffice,
            "Owns the books, the invoices and the statutory filings."),

        new("MIS Analyst", "MIS", 5, Roles.Mis,
            "Builds the reports the management review runs on."),

        new("HR Manager", "HRM", 4, Roles.Hr,
            "Owns hiring, the org chart and attendance."),

        new("Site Engineer", "SEN", 5, null,
            "Reports construction progress from site."),
    ];

    public static async Task SeedAsync(AppDbContext db, int companyId)
    {
        // Matched on name rather than code, because a name is what a person
        // renames and a code is what they leave alone — keying on the code
        // would re-create "Sales Manager" the moment somebody renamed it to
        // "Sales Lead".
        var existing = await db.Designations
            .IgnoreQueryFilters()
            .Where(d => d.CompanyId == companyId)
            .Select(d => d.Name)
            .ToListAsync();

        var have = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        var added = 0;

        foreach (var seed in Defaults)
        {
            if (have.Contains(seed.Name)) continue;

            db.Designations.Add(new Designation
            {
                CompanyId = companyId,
                Name = seed.Name,
                Code = seed.Code,
                Level = seed.Level,
                SuggestedRole = seed.Role,
                Description = seed.Description,
                IsActive = true,
            });

            added++;
        }

        if (added > 0) await db.SaveChangesAsync();
    }
}
