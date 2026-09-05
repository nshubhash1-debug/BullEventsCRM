using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>
/// Works out the tax on one billed amount, and raises the invoice that records it.
///
/// The arithmetic is small and the consequences are not. Under-construction
/// residential is taxed at 5% on two-thirds of the consideration — the other
/// third is deemed to be land, which GST does not reach. Get the abatement
/// wrong and every invoice on the project is wrong by 1.67% of its value, in a
/// direction nobody notices until an assessment.
///
/// The place of supply is the state the building stands in. That is why this
/// splits into CGST and SGST for every buyer, including one who lives in
/// another state and expects IGST — immovable property is supplied where it is.
/// </summary>
public class GstEngine(AppDbContext db, TenantContext tenant)
{
    /// <summary>What one amount comes to, once tax is on it.</summary>
    public readonly record struct Computed(
        string Treatment,
        decimal GrossValue,
        decimal LandAbatement,
        decimal TaxableValue,
        decimal Rate,
        decimal Cgst,
        decimal Sgst,
        decimal Igst)
    {
        public decimal TotalTax => Cgst + Sgst + Igst;
        public decimal Total => GrossValue + TotalTax;
    }

    /// <summary>
    /// Splits an amount into its taxable part and the tax on it.
    ///
    /// Rounded once, at the end, and to the rupee-paise the invoice prints.
    /// Rounding the halves separately and adding them drifts by a paisa on
    /// roughly half of all invoices, which is a reconciliation nobody wins.
    /// </summary>
    public static Computed Compute(string treatment, decimal grossValue, bool interState = false)
    {
        var fraction = GstTreatments.TaxableFraction(treatment);
        var rate = GstTreatments.Rate(treatment);

        var taxable = Math.Round(grossValue * fraction, 2, MidpointRounding.AwayFromZero);
        var abatement = grossValue - taxable;
        var tax = Math.Round(taxable * rate / 100m, 2, MidpointRounding.AwayFromZero);

        // Half each, and the odd paisa goes to CGST — the convention every
        // accounting package in this market follows, so the two agree on
        // reconciliation instead of differing by one paisa per invoice.
        var half = Math.Round(tax / 2m, 2, MidpointRounding.ToZero);

        return new Computed(
            treatment,
            grossValue,
            abatement,
            taxable,
            rate,
            Cgst: interState ? 0m : tax - half,
            Sgst: interState ? 0m : half,
            Igst: interState ? tax : 0m);
    }

    /// <summary>
    /// The tax identity to raise a booking's invoices under.
    ///
    /// The project's own profile first, then the company-wide fallback. A
    /// developer that has configured neither gets told so rather than getting an
    /// invoice with a blank GSTIN, which is not a valid document.
    /// </summary>
    public async Task<GstProfile> ProfileForAsync(int? projectId, CancellationToken ct = default)
    {
        var profile = await db.Set<GstProfile>()
            .Where(p => p.IsActive && (p.ProjectId == projectId || p.ProjectId == null))
            // A project-specific row beats the fallback. Ordering on the
            // nullable directly puts nulls first on Postgres, so compare.
            .OrderByDescending(p => p.ProjectId == projectId)
            .FirstOrDefaultAsync(ct);

        return profile ?? throw ApiException.BadRequest(
            "No GST profile is configured. Set one up under Post-sales → GST profiles "
            + "before raising a tax invoice.");
    }

    /// <summary>
    /// The next invoice number in the project's series.
    ///
    /// Per financial year, which in India runs April to March, because the law
    /// requires the series to be unique within one and everybody reads a number
    /// like 2026-27/0041 as belonging to that year. Collated as "C" because the
    /// database's own collation is non-deterministic and will not do a prefix
    /// match.
    /// </summary>
    public async Task<string> NextInvoiceNumberAsync(
        GstProfile profile, CancellationToken ct = default)
    {
        var stem = $"{profile.InvoicePrefix}/{FinancialYear(DateTime.UtcNow)}/";

        var last = await db.Set<TaxInvoice>()
            .IgnoreQueryFilters()
            .Where(i => i.CompanyId == tenant.CompanyId
                && EF.Functions.Collate(i.InvoiceNumber, "C").StartsWith(stem))
            .OrderByDescending(i => i.InvoiceNumber)
            .Select(i => i.InvoiceNumber)
            .FirstOrDefaultAsync(ct);

        return stem + Next(last, stem).ToString("D4");
    }

    public async Task<string> NextCreditNoteNumberAsync(
        GstProfile profile, CancellationToken ct = default)
    {
        var stem = $"{profile.CreditNotePrefix}/{FinancialYear(DateTime.UtcNow)}/";

        var last = await db.Set<CreditNote>()
            .IgnoreQueryFilters()
            .Where(c => c.CompanyId == tenant.CompanyId
                && EF.Functions.Collate(c.CreditNoteNumber, "C").StartsWith(stem))
            .OrderByDescending(c => c.CreditNoteNumber)
            .Select(c => c.CreditNoteNumber)
            .FirstOrDefaultAsync(ct);

        return stem + Next(last, stem).ToString("D4");
    }

    private static int Next(string? last, string stem) =>
        last is not null && int.TryParse(last[stem.Length..], out var parsed) ? parsed + 1 : 1;

    /// <summary>"2026-27" for any date from 1 April 2026 to 31 March 2027.</summary>
    public static string FinancialYear(DateTime on)
    {
        var start = on.Month >= 4 ? on.Year : on.Year - 1;
        return $"{start}-{(start + 1) % 100:D2}";
    }

    /// <summary>"Q2 2026-27" — the quarter a TDS deduction falls in.</summary>
    public static string Quarter(DateTime on)
    {
        var quarter = on.Month switch
        {
            >= 4 and <= 6 => 1,
            >= 7 and <= 9 => 2,
            >= 10 and <= 12 => 3,
            _ => 4,
        };

        return $"Q{quarter} {FinancialYear(on)}";
    }
}
