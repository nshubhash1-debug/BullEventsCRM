using BullEvents.Api.Models;

namespace BullEvents.Api.Services;

/* ------------------------------------------------------------------ *
 * Inputs and results
 * ------------------------------------------------------------------ */

public record PricingInput
{
    public decimal SaleableArea { get; init; }
    public decimal RatePerSqft { get; init; }
    public decimal PlcPerSqft { get; init; }

    /// <summary>Fraction, not percent — 0.20 for twenty percent.</summary>
    public decimal Discount { get; init; }

    public decimal TaxRate { get; init; } = 0.12m;
    public DateTime BookingDate { get; init; } = DateTime.UtcNow;

    /* ---------------- the event ---------------- */

    /// <summary>
    /// When the event is on. Anchors the pre-event instalments; null on a
    /// proposal for a client who has not fixed a date, whose event-anchored
    /// milestones then print without due dates rather than with wrong ones.
    /// </summary>
    public DateTime? EventDate { get; init; }

    /// <summary>Guests expected. What the per-head lines multiply.</summary>
    public int GuestCount { get; init; }

    /// <summary>
    /// The venue's minimum plate guarantee. Per-head lines bill the higher of
    /// this and <see cref="GuestCount"/>.
    /// </summary>
    public int MinimumPlates { get; init; }

    /// <summary>
    /// The head count the per-head lines actually bill.
    ///
    /// The venue charges for its minimum whether or not the guests turn up, so
    /// a 200-guest wedding in a hall with a 400-plate minimum is a 400-plate
    /// bill. Derived here rather than at each call site so no charge line can
    /// be priced on the guest count alone.
    /// </summary>
    public int BillableHeads => Math.Max(GuestCount, MinimumPlates);

    /// <summary>
    /// The proposal's other lines — catering, décor, photography, the security
    /// deposit. Empty quotes the venue rental alone.
    /// </summary>
    public IReadOnlyList<ChargeInput> Charges { get; init; } = [];
}

/// <summary>One charge head being priced onto this quotation.</summary>
public record ChargeInput
{
    public int? ChargeHeadId { get; init; }
    public int SortOrder { get; init; }
    public string Group { get; init; } = ChargeGroups.UnitCharge;
    public string Name { get; init; } = string.Empty;
    public string Basis { get; init; } = ChargeBases.Lumpsum;
    public decimal Rate { get; init; }

    /// <summary>Slots, KVA, or 1. Ignored by the per-sq-ft and percent bases.</summary>
    public decimal Quantity { get; init; } = 1m;

    public decimal TaxRate { get; init; } = 0.18m;
    public bool IsRefundable { get; init; }
    public bool IncludeInSchedule { get; init; } = true;
    public string? DueLabel { get; init; }
}

public record ChargeResult(
    int? ChargeHeadId,
    int SortOrder,
    string Group,
    string Name,
    string Basis,
    decimal Quantity,
    string? QuantityUnit,
    decimal Rate,
    decimal BasicAmount,
    decimal TaxRate,
    decimal TaxAmount,
    decimal TotalAmount,
    bool IsRefundable,
    bool IncludeInSchedule,
    string? DueLabel);

public record MilestoneResult(
    int SortOrder,
    string Label,
    decimal Percent,
    decimal BasicAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    DateTime? DueDate);

public record PricingResult
{
    public decimal SaleableArea { get; init; }
    public decimal RatePerSqft { get; init; }
    public decimal PlcPerSqft { get; init; }
    public decimal Discount { get; init; }

    /// <summary>Rate after discount with PLC added back — the headline number.</summary>
    public decimal EffectiveRatePerSqft { get; init; }

    /* ---------------- head count ---------------- */

    public int GuestCount { get; init; }
    public int MinimumPlates { get; init; }

    /// <summary>The count the per-head lines were struck on — the higher of the two.</summary>
    public int BilledHeads { get; init; }

    /// <summary>The date priced against, echoed back so callers can snapshot it.</summary>
    public DateTime? EventDate { get; init; }

    /// <summary>True when the guarantee, not the guest count, set the bill.</summary>
    public bool MinimumApplied => MinimumPlates > GuestCount;

    /// <summary>The venue rental alone — what the milestone percentages are struck on.</summary>
    public decimal BasicAmount { get; init; }
    public decimal TaxRate { get; init; }
    public decimal TaxAmount { get; init; }
    public decimal TotalAmount { get; init; }

    /* ---------------- other charge heads ---------------- */

    public IReadOnlyList<ChargeResult> Charges { get; init; } = [];

    public decimal ChargesBasic { get; init; }
    public decimal ChargesTax { get; init; }
    public decimal ChargesTotal { get; init; }

    /// <summary>Of the charges, the part that is a refundable deposit.</summary>
    public decimal RefundableTotal { get; init; }

    /// <summary>Unit cost plus every other head.</summary>
    public decimal GrandTotal { get; init; }

    /// <summary>The part the payment plan spreads across its instalments.</summary>
    public decimal ScheduledTotal { get; init; }

    /// <summary>What falls due on its own terms rather than through the plan.</summary>
    public decimal UnscheduledTotal { get; init; }

    public string AmountInWords { get; init; } = string.Empty;
    public string GrandTotalInWords { get; init; } = string.Empty;
    public IReadOnlyList<MilestoneResult> Milestones { get; init; } = [];

    /// <summary>
    /// Schedule total minus consideration. Always presented rather than
    /// asserted away: a plan whose percentages do not close is a data problem
    /// the pricing desk needs to see, not something to silently absorb.
    /// </summary>
    public decimal ScheduleVariance { get; init; }
}

/* ------------------------------------------------------------------ *
 * Engine
 * ------------------------------------------------------------------ */

/// <summary>
/// The quotation arithmetic, lifted out of the pricing workbook this system
/// replaces.
///
/// Two things about it are easy to get wrong and are therefore stated once,
/// here, rather than in each caller:
///
/// 1. PLC is not discounted. The discount applies to the basic rate only and
///    the preferential-location charge is added back afterwards, so a 20% plan
///    on a ₹23,400 rate with ₹3,000 PLC gives ₹21,720 — not ₹21,120.
///
/// 2. Milestone percentages are applied to the basic and the tax separately and
///    then summed, never to the gross. Applying them to the gross drifts by
///    paise per line, which on a nineteen-line construction-linked plan shows
///    up as a schedule that does not tie back to the consideration.
/// </summary>
public static class QuotationPricing
{
    /// <summary>Money is carried to paise; anything finer is rounding noise.</summary>
    private static decimal Money(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// How much per-line rounding drift the last instalment may absorb.
    ///
    /// Rounding nineteen lines to paise can leave the schedule a few paise off
    /// the consideration, and a payment plan that does not tie back is a
    /// document nobody can reconcile. One rupee is far more than that drift can
    /// ever reach and far less than any real misconfiguration — a plan whose
    /// percentages sum to 90% is off by lakhs and still gets reported.
    /// </summary>
    private const decimal RoundingTolerance = 1m;

    public static PricingResult Calculate(
        PricingInput input,
        IReadOnlyList<PaymentPlanMilestone> milestones)
    {
        var effectiveRate = Money(
            (input.RatePerSqft - (input.RatePerSqft * input.Discount)) + input.PlcPerSqft);

        var basic = Money(input.SaleableArea * effectiveRate);
        var tax = Money(basic * input.TaxRate);
        var total = basic + tax;

        var charges = PriceCharges(input, basic);

        // The plan spreads the unit cost plus whichever heads are marked
        // scheduled; the rest — the maintenance deposit, the registration fee —
        // falls due on its own terms and is listed separately so adding one can
        // never quietly move every instalment.
        var scheduledCharges = charges.Where(c => c.IncludeInSchedule).ToList();
        var scheduledBasic = basic + scheduledCharges.Sum(c => c.BasicAmount);
        var scheduledTax = tax + scheduledCharges.Sum(c => c.TaxAmount);
        var scheduledTotal = scheduledBasic + scheduledTax;

        var schedule = BuildSchedule(input, milestones, scheduledBasic, scheduledTax, scheduledTotal);
        var variance = Money(schedule.Sum(m => m.TotalAmount) - scheduledTotal);

        if (variance != 0 && Math.Abs(variance) <= RoundingTolerance && schedule.Count > 0)
        {
            // The last instalment carries the difference. Putting it anywhere
            // else would mean the customer's final payment does not clear the
            // balance, which is the one line that must be exact.
            var last = schedule[^1];
            schedule[^1] = last with
            {
                BasicAmount = last.BasicAmount - variance,
                TotalAmount = last.TotalAmount - variance,
            };

            variance = 0m;
        }

        var chargesBasic = charges.Sum(c => c.BasicAmount);
        var chargesTax = charges.Sum(c => c.TaxAmount);
        var chargesTotal = chargesBasic + chargesTax;
        var grandTotal = total + chargesTotal;

        return new PricingResult
        {
            SaleableArea = input.SaleableArea,
            RatePerSqft = input.RatePerSqft,
            PlcPerSqft = input.PlcPerSqft,
            Discount = input.Discount,
            EffectiveRatePerSqft = effectiveRate,
            GuestCount = input.GuestCount,
            MinimumPlates = input.MinimumPlates,
            BilledHeads = input.BillableHeads,
            EventDate = input.EventDate,
            BasicAmount = basic,
            TaxRate = input.TaxRate,
            TaxAmount = tax,
            TotalAmount = total,

            Charges = charges,
            ChargesBasic = chargesBasic,
            ChargesTax = chargesTax,
            ChargesTotal = chargesTotal,
            RefundableTotal = charges.Where(c => c.IsRefundable).Sum(c => c.TotalAmount),
            GrandTotal = grandTotal,
            ScheduledTotal = scheduledTotal,
            UnscheduledTotal = grandTotal - scheduledTotal,

            AmountInWords = IndianNumberWords.ToWords(total),
            GrandTotalInWords = IndianNumberWords.ToWords(grandTotal),
            Milestones = schedule,
            ScheduleVariance = variance,
        };
    }

    /// <summary>
    /// Prices the non-unit heads.
    ///
    /// Each carries its own GST rather than the plan's: a club membership is
    /// taxed at 18% on the same document where the flat is taxed at 12%, and
    /// applying one rate to both is the mistake that makes a cost sheet fail
    /// reconciliation against the tax return.
    /// </summary>
    private static List<ChargeResult> PriceCharges(PricingInput input, decimal unitBasic)
    {
        var results = new List<ChargeResult>();

        foreach (var charge in input.Charges.OrderBy(c => c.SortOrder))
        {
            decimal quantity;
            string? quantityUnit;
            decimal lineBasic;

            switch (charge.Basis)
            {
                case ChargeBases.PerSqft:
                    quantity = input.SaleableArea;
                    quantityUnit = "sq ft";
                    lineBasic = Money(input.SaleableArea * charge.Rate);
                    break;

                case ChargeBases.PerQuantity:
                    quantity = charge.Quantity;
                    quantityUnit = "nos";
                    lineBasic = Money(charge.Quantity * charge.Rate);
                    break;

                case ChargeBases.PerGuest:
                    // Billable heads, not guests: the minimum guarantee is what
                    // the venue actually invoices. Carried into the result so
                    // the printed line shows the count it was struck on and the
                    // client can see why 200 guests cost 400 plates.
                    quantity = input.BillableHeads;
                    quantityUnit = "plates";
                    lineBasic = Money(input.BillableHeads * charge.Rate);
                    break;

                case ChargeBases.PercentOfUnitCost:
                    quantity = 1m;
                    quantityUnit = null;
                    lineBasic = Money(unitBasic * charge.Rate);
                    break;

                default: // Lumpsum
                    quantity = 1m;
                    quantityUnit = null;
                    lineBasic = Money(charge.Rate);
                    break;
            }

            if (lineBasic == 0) continue;

            var lineTax = Money(lineBasic * charge.TaxRate);

            results.Add(new ChargeResult(
                charge.ChargeHeadId,
                charge.SortOrder,
                charge.Group,
                charge.Name,
                charge.Basis,
                quantity,
                quantityUnit,
                charge.Rate,
                lineBasic,
                charge.TaxRate,
                lineTax,
                lineBasic + lineTax,
                charge.IsRefundable,
                charge.IncludeInSchedule,
                charge.DueLabel));
        }

        return results;
    }

    private static List<MilestoneResult> BuildSchedule(
        PricingInput input,
        IReadOnlyList<PaymentPlanMilestone> milestones,
        decimal basic,
        decimal tax,
        decimal total)
    {
        var ordered = milestones.OrderBy(m => m.SortOrder).ToList();
        if (ordered.Count == 0) return [];

        // The fixed instalments come out first, because two of the four bases
        // are defined relative to what is left after them. Their basic/tax split
        // is derived from the gross at the prevailing rate — an EOI is quoted to
        // the customer as one round number, not as a basic plus a tax.
        var fixedGross = ordered
            .Where(m => m.Basis == MilestoneBases.Fixed)
            .Sum(m => m.FixedAmount);

        var netBasic = basic - Money(fixedGross / (1 + input.TaxRate));
        var netTax = tax - (fixedGross - Money(fixedGross / (1 + input.TaxRate)));

        var results = new List<MilestoneResult>(ordered.Count);
        var runningTotal = 0m;

        foreach (var milestone in ordered)
        {
            decimal lineBasic, lineTax;

            switch (milestone.Basis)
            {
                case MilestoneBases.Fixed:
                    lineBasic = Money(milestone.FixedAmount / (1 + input.TaxRate));
                    lineTax = milestone.FixedAmount - lineBasic;
                    break;

                case MilestoneBases.PercentOfNetOfFixed:
                    lineBasic = Money(netBasic * milestone.Percent);
                    lineTax = Money(netTax * milestone.Percent);
                    break;

                case MilestoneBases.BalanceToPercent:
                {
                    // Bring the running total up to this share of the whole.
                    // Clamped at zero so an over-collected schedule produces a
                    // nil line rather than a negative instalment.
                    var target = Money(total * milestone.Percent);
                    var due = Math.Max(0m, target - runningTotal);

                    lineBasic = Money(due / (1 + input.TaxRate));
                    lineTax = due - lineBasic;
                    break;
                }

                default: // PercentOfTotal
                    lineBasic = Money(basic * milestone.Percent);
                    lineTax = Money(tax * milestone.Percent);
                    break;
            }

            var lineTotal = lineBasic + lineTax;
            runningTotal += lineTotal;

            results.Add(new MilestoneResult(
                milestone.SortOrder,
                milestone.Label,
                total == 0 ? 0 : decimal.Round(lineTotal / total, 6),
                lineBasic,
                lineTax,
                lineTotal,
                MilestoneAnchors.Resolve(
                    milestone.DueAnchor,
                    milestone.DueOffsetDays,
                    input.BookingDate.Date,
                    input.EventDate?.Date)));
        }

        return results;
    }
}

/* ------------------------------------------------------------------ *
 * Number to words
 * ------------------------------------------------------------------ */

/// <summary>
/// Indian-system number-to-words, matching what the pricing workbook printed on
/// every quotation: lakh and crore rather than million and billion, and the
/// larger arab / kharab / neel places above them.
///
/// Kept as its own type because the amount in words is a legal nicety on a
/// priced offer — the figure and the words disagreeing is the kind of defect
/// that voids a document.
/// </summary>
public static class IndianNumberWords
{
    private static readonly string[] Ones =
    [
        "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine",
        "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen",
        "Seventeen", "Eighteen", "Nineteen"
    ];

    private static readonly string[] Tens =
    [
        "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"
    ];

    private static readonly string[] Digits =
    [
        "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine"
    ];

    private static string TwoDigit(int value) => value switch
    {
        0 => "",
        < 20 => Ones[value],
        _ => Tens[value / 10] + (value % 10 > 0 ? " " + Ones[value % 10] : ""),
    };

    private static string ThreeDigit(int value) => value switch
    {
        0 => "",
        >= 100 => $"{Ones[value / 100]} Hundred{(value % 100 > 0 ? " " + TwoDigit(value % 100) : "")}",
        _ => TwoDigit(value),
    };

    /// <summary>
    /// "One Crore, Seventy Two Lakh, Ninety Seven Thousand, Two Hundred Eighty".
    /// Paise, when there are any, are read digit by digit after "Point" — the
    /// convention the workbook used and the one Indian invoices follow.
    /// </summary>
    public static string ToWords(decimal amount)
    {
        if (amount == 0) return "Zero";

        var negative = amount < 0;
        amount = Math.Abs(amount);

        var whole = decimal.Truncate(amount);
        var paise = (int)decimal.Round((amount - whole) * 100, 0, MidpointRounding.AwayFromZero);

        // Rounding the fraction can carry into the rupees; without this,
        // 99.999 reads as "Ninety Nine Point Zero Zero".
        if (paise == 100)
        {
            whole += 1;
            paise = 0;
        }

        if (whole > 999_999_999_999_999m) return "Amount out of range";

        var padded = ((long)whole).ToString("000000000000000");

        var parts = new List<string>();

        void Place(int start, int length, string name)
        {
            var value = int.Parse(padded.Substring(start, length));
            if (value == 0) return;

            parts.Add(length == 3
                ? ThreeDigit(value) + (name.Length > 0 ? " " + name : "")
                : TwoDigit(value) + " " + name);
        }

        Place(0, 2, "Neel");
        Place(2, 2, "Kharab");
        Place(4, 2, "Arab");
        Place(6, 2, "Crore");
        Place(8, 2, "Lakh");
        Place(10, 2, "Thousand");
        Place(12, 3, "");

        var words = string.Join(", ", parts);

        if (paise > 0)
        {
            words += $" Point {Digits[paise / 10]} {Digits[paise % 10]}";
        }

        return (negative ? "Minus " : "") + words;
    }

    /// <summary>The full line as it prints — "Rupees … Only".</summary>
    public static string ToRupees(decimal amount) => $"Rupees {ToWords(amount)} Only";
}
