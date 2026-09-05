namespace BullEvents.Api.Services;

/* ------------------------------------------------------------------ *
 * Inputs
 * ------------------------------------------------------------------ */

/// <summary>
/// What the return schedule is being computed from.
///
/// Every figure is passed in rather than read off the payment plan, because a
/// return is a commercial concession the desk makes on one deal, not a property
/// of the plan. The plan supplies the defaults; the rep decides whether this
/// buyer is being offered an assured return at all, and at what rate.
/// </summary>
public record ReturnsInput
{
    /// <summary>The basic sale price the return accrues on — never the gross.</summary>
    public decimal BasicSalePrice { get; init; }
    public decimal SaleableArea { get; init; }

    public bool IncludeAssuredReturn { get; init; }
    public bool IncludeBuyBack { get; init; }
    public bool IncludeRentalYield { get; init; }

    /// <summary>Assured annual return as a fraction of BSP — 0.09 for nine percent.</summary>
    public decimal AssuredReturnPercent { get; init; }

    /// <summary>Years the assured return runs, usually to possession. Halves are allowed.</summary>
    public decimal AssuredReturnYears { get; init; }

    /// <summary>Buy-back appreciation as a fraction of BSP per year.</summary>
    public decimal BuyBackPercentPerYear { get; init; }

    /// <summary>Years after full payment before the buy-back may be exercised.</summary>
    public decimal BuyBackEligibleAfterYears { get; init; }

    /// <summary>
    /// Years the buy-back accrues over. Distinct from the eligibility window:
    /// eligibility says when the option opens, this says what it is worth by
    /// then. Zero falls back to the assured horizon, then to eligibility.
    /// </summary>
    public decimal BuyBackHorizonYears { get; init; }

    public decimal RentPerSqftPerMonth { get; init; }

    /// <summary>Printed under the annexure — the conditions the offer is subject to.</summary>
    public string? Conditions { get; init; }

    /// <summary>Year one of the schedule counts from here.</summary>
    public DateTime StartDate { get; init; } = DateTime.UtcNow;
}

/* ------------------------------------------------------------------ *
 * Result
 * ------------------------------------------------------------------ */

/// <summary>One year of the projection, as it prints.</summary>
public record ReturnYearRow(
    int Year,
    DateTime PeriodEnd,
    /// <summary>Less than one on a part year — a 4.5-year horizon ends on a half.</summary>
    decimal YearFraction,
    decimal AssuredReturn,
    decimal RentalIncome,
    decimal CumulativeReturn);

/// <summary>
/// The return schedule the up-front plans are sold on.
///
/// Computed once and then snapshotted onto the quotation, never recomputed on
/// read: the plan's rates move every quarter, and an offer already with a
/// customer has to keep saying what it said when it was sent.
/// </summary>
public record InvestorAnnexure
{
    public decimal BasicSalePrice { get; init; }

    public bool HasAssuredReturn { get; init; }
    public bool HasBuyBack { get; init; }
    public bool HasRentalYield { get; init; }

    public decimal AssuredReturnPercent { get; init; }
    public decimal AssuredReturnYears { get; init; }

    /// <summary>What one full year of the assured return pays.</summary>
    public decimal AssuredReturnPerYear { get; init; }
    public decimal AssuredReturnPerMonth { get; init; }
    public decimal AssuredReturnAmount { get; init; }

    public decimal BuyBackPercentPerYear { get; init; }
    public decimal BuyBackEligibleAfterYears { get; init; }
    public decimal BuyBackHorizonYears { get; init; }
    public decimal BuyBackAmount { get; init; }

    /// <summary>BSP plus the accrued appreciation — what the developer buys back at.</summary>
    public decimal BuyBackValue { get; init; }

    public decimal IndicativeRentPerSqftPerMonth { get; init; }
    public decimal IndicativeRentPerMonth { get; init; }
    public decimal IndicativeRentPerYear { get; init; }

    /// <summary>A year of rent over the BSP — the yield an investor compares against.</summary>
    public decimal GrossRentalYield { get; init; }

    /// <summary>
    /// Assured return plus buy-back appreciation. Rent is an alternative to the
    /// assured return rather than an addition, so it is deliberately not here.
    /// </summary>
    public decimal TotalEarned { get; init; }

    /// <summary>Total earned over the BSP.</summary>
    public decimal ReturnOnInvestment { get; init; }

    /// <summary>The same, divided by the horizon — comparable to a deposit rate.</summary>
    public decimal AnnualisedReturn { get; init; }

    /// <summary>The horizon the ROI is stated over, in years.</summary>
    public decimal HorizonYears { get; init; }

    public IReadOnlyList<ReturnYearRow> Schedule { get; init; } = [];
    public string? Conditions { get; init; }

    /// <summary>False when nothing was switched on — the annexure is then not printed.</summary>
    public bool HasAnything => HasAssuredReturn || HasBuyBack || HasRentalYield;
}

/* ------------------------------------------------------------------ *
 * Engine
 * ------------------------------------------------------------------ */

/// <summary>
/// The investment arithmetic printed beside the price on the up-front plans.
///
/// Three rules the workbook follows and every caller here inherits:
///
/// 1. Everything accrues on the basic sale price, not on the consideration. GST
///    and the maintenance deposit are not the investor's capital in the unit,
///    and paying a return on them would overstate the offer by a sixth.
///
/// 2. The assured return and the indicative rent are alternatives, never a sum.
///    The developer pays one or a tenant pays the other; adding them together
///    would sell the same square foot twice.
///
/// 3. Buy-back is appreciation on top of capital returned. The buy-back value is
///    BSP plus the accrual — quoting the accrual alone reads as though the buyer
///    forfeits their principal.
/// </summary>
public static class InvestorReturns
{
    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    private static decimal Rate(decimal value) => decimal.Round(value, 6, MidpointRounding.AwayFromZero);

    /// <summary>
    /// A quotation may run a return for a decade, but nobody sells one longer,
    /// and an unbounded loop over a mistyped horizon is how a PDF request hangs.
    /// </summary>
    private const int MaxScheduleYears = 30;

    public static InvestorAnnexure Calculate(ReturnsInput input)
    {
        var bsp = Math.Max(0m, input.BasicSalePrice);

        var assuredOn = input.IncludeAssuredReturn
            && input.AssuredReturnPercent > 0
            && input.AssuredReturnYears > 0;

        var buyBackOn = input.IncludeBuyBack && input.BuyBackPercentPerYear > 0;
        var rentOn = input.IncludeRentalYield && input.RentPerSqftPerMonth > 0;

        var assuredPerYear = assuredOn ? Money(bsp * input.AssuredReturnPercent) : 0m;
        var assuredAmount = assuredOn
            ? Money(bsp * input.AssuredReturnPercent * input.AssuredReturnYears)
            : 0m;

        // Eligibility says when the option opens; the horizon says what it is
        // worth by then. Falling back to the assured horizon keeps a plan that
        // configures only one of the two producing the workbook's figure.
        var buyBackYears = input.BuyBackHorizonYears > 0
            ? input.BuyBackHorizonYears
            : input.AssuredReturnYears > 0
                ? input.AssuredReturnYears
                : input.BuyBackEligibleAfterYears;

        var buyBackAmount = buyBackOn
            ? Money(bsp * input.BuyBackPercentPerYear * buyBackYears)
            : 0m;

        var rentPerMonth = rentOn ? Money(input.SaleableArea * input.RentPerSqftPerMonth) : 0m;
        var rentPerYear = Money(rentPerMonth * 12);

        var totalEarned = assuredAmount + buyBackAmount;

        var horizon = assuredOn
            ? input.AssuredReturnYears
            : buyBackOn ? buyBackYears : 0m;

        return new InvestorAnnexure
        {
            BasicSalePrice = bsp,

            HasAssuredReturn = assuredOn,
            HasBuyBack = buyBackOn,
            HasRentalYield = rentOn,

            AssuredReturnPercent = assuredOn ? input.AssuredReturnPercent : 0m,
            AssuredReturnYears = assuredOn ? input.AssuredReturnYears : 0m,
            AssuredReturnPerYear = assuredPerYear,
            AssuredReturnPerMonth = Money(assuredPerYear / 12),
            AssuredReturnAmount = assuredAmount,

            BuyBackPercentPerYear = buyBackOn ? input.BuyBackPercentPerYear : 0m,
            BuyBackEligibleAfterYears = buyBackOn ? input.BuyBackEligibleAfterYears : 0m,
            BuyBackHorizonYears = buyBackOn ? buyBackYears : 0m,
            BuyBackAmount = buyBackAmount,
            BuyBackValue = buyBackOn ? bsp + buyBackAmount : 0m,

            IndicativeRentPerSqftPerMonth = rentOn ? input.RentPerSqftPerMonth : 0m,
            IndicativeRentPerMonth = rentPerMonth,
            IndicativeRentPerYear = rentPerYear,
            GrossRentalYield = rentOn && bsp > 0 ? Rate(rentPerYear / bsp) : 0m,

            TotalEarned = totalEarned,
            ReturnOnInvestment = bsp > 0 ? Rate(totalEarned / bsp) : 0m,
            AnnualisedReturn = bsp > 0 && horizon > 0 ? Rate(totalEarned / bsp / horizon) : 0m,
            HorizonYears = horizon,

            Schedule = BuildSchedule(input, assuredOn, rentOn, assuredPerYear, rentPerYear),
            Conditions = string.IsNullOrWhiteSpace(input.Conditions) ? null : input.Conditions.Trim(),
        };
    }

    /// <summary>
    /// Year one to the end of the horizon, with the last year pro-rated.
    ///
    /// A 4.5-year assured return pays five instalments, the last of them half —
    /// rounding it up to five full years overstates the offer by half a year's
    /// return, which on a one-crore unit is four and a half lakh the developer
    /// never promised.
    /// </summary>
    private static List<ReturnYearRow> BuildSchedule(
        ReturnsInput input, bool assuredOn, bool rentOn,
        decimal assuredPerYear, decimal rentPerYear)
    {
        if (!assuredOn && !rentOn) return [];

        var horizon = assuredOn
            ? input.AssuredReturnYears
            : Math.Max(1m, decimal.Truncate(input.BuyBackHorizonYears));

        if (horizon <= 0) return [];

        var years = Math.Min(MaxScheduleYears, (int)Math.Ceiling(horizon));
        var rows = new List<ReturnYearRow>(years);
        var cumulative = 0m;

        for (var year = 1; year <= years; year++)
        {
            var fraction = Math.Min(1m, horizon - (year - 1));
            if (fraction <= 0) break;

            var assured = assuredOn ? Money(assuredPerYear * fraction) : 0m;
            var rent = rentOn ? Money(rentPerYear * fraction) : 0m;

            // Cumulative follows whichever stream is being promised. Where both
            // are on, the assured return is the promise and the rent prints
            // beside it as the alternative — summing them would double-count.
            cumulative += assuredOn ? assured : rent;

            rows.Add(new ReturnYearRow(
                year,
                input.StartDate.Date.AddMonths(year * 12),
                decimal.Round(fraction, 2),
                assured,
                rent,
                cumulative));
        }

        return rows;
    }
}
