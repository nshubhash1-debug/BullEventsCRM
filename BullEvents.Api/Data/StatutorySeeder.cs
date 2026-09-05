using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Data;

/// <summary>
/// The statutory rates an Indian employer starts from.
///
/// Seeded rather than hardcoded so a payroll clerk can correct a slab when a
/// state revises one, without waiting for a deploy. Runs once — the moment a
/// statutory config exists on the tenant it does nothing, so a company that has
/// tuned its own rates is never overwritten.
///
/// <b>These are a starting point, not legal advice.</b> Rates and slabs are
/// reproduced from the position for FY 2026-27 as published; professional tax
/// in particular is state legislation that moves without much notice, and the
/// rows for states this company does not operate in have not been checked as
/// carefully as the ones for those it does. They want reviewing against the
/// current notifications before the first live run.
/// </summary>
public static class StatutorySeeder
{
    /// <summary>Financial year this seed describes — 2026 means FY 2026-27.</summary>
    private const int FinancialYear = 2026;

    /* ------------------------------------------------------------------ *
     * Professional tax
     *
     * Flat amounts against monthly gross bands, per state. Only the states with
     * a meaningful workforce presence are seeded in full; the rest are the
     * common two-band shape and want checking before use.
     * ------------------------------------------------------------------ */

    private record PtBand(decimal From, decimal? To, decimal Amount, int? Month = null);

    private static readonly Dictionary<string, PtBand[]> ProfessionalTax = new()
    {
        // Maharashtra: ₹200 a month, except February at ₹300, which brings the
        // year to the ₹2,500 cap. Women are exempt below ₹25,000.
        [PtStates.Maharashtra] =
        [
            new(0m, 7_500m, 0m),
            new(7_500.01m, 10_000m, 175m),
            new(10_000.01m, null, 200m),
            new(10_000.01m, null, 300m, Month: 2),
        ],

        [PtStates.Karnataka] =
        [
            new(0m, 24_999m, 0m),
            new(25_000m, null, 200m),
        ],

        [PtStates.WestBengal] =
        [
            new(0m, 10_000m, 0m),
            new(10_000.01m, 15_000m, 110m),
            new(15_000.01m, 25_000m, 130m),
            new(25_000.01m, 40_000m, 150m),
            new(40_000.01m, null, 200m),
        ],

        [PtStates.TamilNadu] =
        [
            new(0m, 21_000m, 0m),
            new(21_000.01m, 30_000m, 135m),
            new(30_000.01m, 45_000m, 315m),
            new(45_000.01m, 60_000m, 690m),
            new(60_000.01m, 75_000m, 1_025m),
            new(75_000.01m, null, 1_250m),
        ],

        [PtStates.AndhraPradesh] =
        [
            new(0m, 15_000m, 0m),
            new(15_000.01m, 20_000m, 150m),
            new(20_000.01m, null, 200m),
        ],

        [PtStates.Telangana] =
        [
            new(0m, 15_000m, 0m),
            new(15_000.01m, 20_000m, 150m),
            new(20_000.01m, null, 200m),
        ],

        [PtStates.Gujarat] =
        [
            new(0m, 12_000m, 0m),
            new(12_000.01m, null, 200m),
        ],

        [PtStates.MadhyaPradesh] =
        [
            new(0m, 18_750m, 0m),
            new(18_750.01m, 25_000m, 125m),
            new(25_000.01m, 33_333m, 167m),
            new(33_333.01m, null, 208m),
            new(33_333.01m, null, 212m, Month: 2),
        ],

        [PtStates.Kerala] =
        [
            new(0m, 11_999m, 0m),
            new(12_000m, 17_999m, 120m),
            new(18_000m, 29_999m, 180m),
            new(30_000m, 44_999m, 300m),
            new(45_000m, 59_999m, 450m),
            new(60_000m, 74_999m, 600m),
            new(75_000m, 99_999m, 750m),
            new(100_000m, 124_999m, 1_000m),
            new(125_000m, null, 1_250m),
        ],

        [PtStates.Odisha] =
        [
            new(0m, 13_304m, 0m),
            new(13_304.01m, 25_000m, 125m),
            new(25_000.01m, null, 200m),
            new(25_000.01m, null, 300m, Month: 2),
        ],

        [PtStates.Assam] =
        [
            new(0m, 10_000m, 0m),
            new(10_000.01m, 15_000m, 150m),
            new(15_000.01m, 25_000m, 180m),
            new(25_000.01m, null, 208m),
        ],

        [PtStates.Bihar] =
        [
            new(0m, 25_000m, 0m),
            new(25_000.01m, 41_666m, 83.33m),
            new(41_666.01m, 83_333m, 166.67m),
            new(83_333.01m, null, 208.33m),
        ],

        [PtStates.Jharkhand] =
        [
            new(0m, 25_000m, 0m),
            new(25_000.01m, 41_666m, 100m),
            new(41_666.01m, 66_666m, 150m),
            new(66_666.01m, 83_333m, 175m),
            new(83_333.01m, null, 208m),
        ],

        [PtStates.Meghalaya] =
        [
            new(0m, 4_166m, 0m),
            new(4_166.01m, 6_250m, 16.50m),
            new(6_250.01m, 8_333m, 25m),
            new(8_333.01m, 12_500m, 41.50m),
            new(12_500.01m, 16_666m, 62.50m),
            new(16_666.01m, 20_833m, 83.33m),
            new(20_833.01m, 25_000m, 104.16m),
            new(25_000.01m, 29_166m, 125m),
            new(29_166.01m, 33_333m, 150m),
            new(33_333.01m, 37_500m, 175m),
            new(37_500.01m, 41_666m, 200m),
            new(41_666.01m, null, 208m),
        ],

        [PtStates.Tripura] =
        [
            new(0m, 7_500m, 0m),
            new(7_500.01m, 15_000m, 150m),
            new(15_000.01m, 25_000m, 200m),
            new(25_000.01m, null, 208m),
        ],

        [PtStates.Puducherry] =
        [
            new(0m, 16_666m, 0m),
            new(16_666.01m, 33_333m, 41.66m),
            new(33_333.01m, 50_000m, 83.33m),
            new(50_000.01m, 66_666m, 125m),
            new(66_666.01m, null, 166.66m),
        ],

        [PtStates.Sikkim] =
        [
            new(0m, 20_000m, 0m),
            new(20_000.01m, 30_000m, 125m),
            new(30_000.01m, 40_000m, 150m),
            new(40_000.01m, null, 200m),
        ],

        [PtStates.Nagaland] =
        [
            new(0m, 4_000m, 0m),
            new(4_000.01m, 5_000m, 35m),
            new(5_000.01m, 7_000m, 75m),
            new(7_000.01m, 9_000m, 110m),
            new(9_000.01m, 12_000m, 180m),
            new(12_000.01m, null, 208m),
        ],

        [PtStates.Manipur] =
        [
            new(0m, 4_166m, 0m),
            new(4_166.01m, 6_250m, 100m),
            new(6_250.01m, 8_333m, 167m),
            new(8_333.01m, 10_416m, 200m),
            new(10_416.01m, null, 208m),
        ],

        [PtStates.Mizoram] =
        [
            new(0m, 5_000m, 0m),
            new(5_000.01m, 8_000m, 75m),
            new(8_000.01m, 10_000m, 120m),
            new(10_000.01m, 12_000m, 150m),
            new(12_000.01m, 15_000m, 180m),
            new(15_000.01m, null, 208m),
        ],
    };

    /* ------------------------------------------------------------------ *
     * Income tax, FY 2026-27
     * ------------------------------------------------------------------ */

    private record Slab(decimal From, decimal? To, decimal Rate);

    /// <summary>
    /// The default regime since FY 2023-24. Almost no deductions, wider bands,
    /// a ₹75,000 standard deduction and an 87A rebate that takes tax to nil up
    /// to ₹12 lakh of taxable income.
    /// </summary>
    private static readonly Slab[] NewRegime =
    [
        new(0m, 400_000m, 0m),
        new(400_000m, 800_000m, 0.05m),
        new(800_000m, 1_200_000m, 0.10m),
        new(1_200_000m, 1_600_000m, 0.15m),
        new(1_600_000m, 2_000_000m, 0.20m),
        new(2_000_000m, 2_400_000m, 0.25m),
        new(2_400_000m, null, 0.30m),
    ];

    /// <summary>
    /// The regime you elect into when your deductions are worth more than the
    /// wider bands — a home loan, a full 80C, and metro rent usually decide it.
    /// </summary>
    private static readonly Slab[] OldRegime =
    [
        new(0m, 250_000m, 0m),
        new(250_000m, 500_000m, 0.05m),
        new(500_000m, 1_000_000m, 0.20m),
        new(1_000_000m, null, 0.30m),
    ];

    public static async Task SeedAsync(AppDbContext db)
    {
        // One statutory row anywhere means this tenant has been set up. Never
        // overwrite rates somebody has tuned.
        if (await db.HrStatutoryConfigs.IgnoreQueryFilters().AnyAsync()) return;

        var company = await db.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync();
        if (company is null) return;

        /* ---------------- the rate set ---------------- */

        db.HrStatutoryConfigs.Add(new HrStatutoryConfig
        {
            CompanyId = company.Id,
            EffectiveFrom = new DateOnly(FinancialYear, 4, 1),
            Label = $"FY {FinancialYear}-{(FinancialYear + 1) % 100:D2} statutory rates",
            PtDefaultState = PtStates.Maharashtra,
            LwfEnabled = false,
            Notes = "Seeded defaults. Review against the current notifications "
                + "before the first live payroll run.",
        });

        /* ---------------- professional tax ---------------- */

        foreach (var (state, bands) in ProfessionalTax)
        {
            foreach (var band in bands)
            {
                db.HrProfessionalTaxSlabs.Add(new HrProfessionalTaxSlab
                {
                    CompanyId = company.Id,
                    State = state,
                    FromAmount = band.From,
                    ToAmount = band.To,
                    Amount = band.Amount,
                    Month = band.Month,
                    EffectiveFrom = new DateOnly(FinancialYear, 4, 1),
                });
            }
        }

        /* ---------------- income tax ---------------- */

        void AddSlabs(string regime, Slab[] slabs)
        {
            foreach (var (slab, index) in slabs.Select((s, i) => (s, i)))
            {
                db.HrIncomeTaxSlabs.Add(new HrIncomeTaxSlab
                {
                    CompanyId = company.Id,
                    FinancialYear = FinancialYear,
                    Regime = regime,
                    FromAmount = slab.From,
                    ToAmount = slab.To,
                    Rate = slab.Rate,
                    SortOrder = index,
                });
            }
        }

        AddSlabs(TaxRegimes.New, NewRegime);
        AddSlabs(TaxRegimes.Old, OldRegime);

        db.HrTaxRegimeConfigs.AddRange(
            new HrTaxRegimeConfig
            {
                CompanyId = company.Id,
                FinancialYear = FinancialYear,
                Regime = TaxRegimes.New,
                StandardDeduction = 75_000m,
                RebateIncomeCeiling = 1_200_000m,
                RebateMaximum = 60_000m,
                AllowsChapterViaDeductions = false,
                AllowsHraExemption = false,
                // The new regime's surcharge stops at 25%; the 37% band was
                // removed when it became the default.
                SurchargeBands = "5000000:0.10,10000000:0.15,20000000:0.25",
            },
            new HrTaxRegimeConfig
            {
                CompanyId = company.Id,
                FinancialYear = FinancialYear,
                Regime = TaxRegimes.Old,
                StandardDeduction = 50_000m,
                RebateIncomeCeiling = 500_000m,
                RebateMaximum = 12_500m,
                AllowsChapterViaDeductions = true,
                AllowsHraExemption = true,
                SurchargeBands = "5000000:0.10,10000000:0.15,20000000:0.25,50000000:0.37",
            });

        await db.SaveChangesAsync();
    }
}
