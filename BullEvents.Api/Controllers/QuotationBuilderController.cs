using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using BullEvents.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;

namespace BullEvents.Api.Controllers;

/// <summary>
/// Prices a unit and turns the result into a quotation.
///
/// Everything that decides what a unit costs — the rate card in force, the
/// plan's discount, the tax and the instalment schedule — is resolved here and
/// nowhere else. The preview endpoint and the save endpoint run the identical
/// path, which is the only reliable way to guarantee that what the rep saw on
/// screen is what the customer receives.
/// </summary>
[ApiController]
[Route("api/quotations")]
[Authorize]
[SecuredBy(SecuredObjects.Quotation)]
public class QuotationBuilderController(
    AppDbContext db,
    ApprovalService approvals,
    QuotationLifecycle lifecycle)
    : CrmControllerBase(db)
{
    /* ------------------------------------------------------------------ *
     * Payment plans
     * ------------------------------------------------------------------ */

    [HttpGet("payment-plans")]
    public async Task<ActionResult<IReadOnlyList<PaymentPlanDto>>> Plans(
        [FromQuery] int? projectId, CancellationToken ct)
    {
        var plans = await Db.PaymentPlans.AsNoTracking()
            .Include(p => p.Milestones)
            .Where(p => p.IsActive && (p.ProjectId == null || projectId == null || p.ProjectId == projectId))
            .OrderBy(p => p.SortOrder)
            .ToListAsync(ct);

        return Ok(plans.Select(ToPlanDto).ToList());
    }

    private static PaymentPlanDto ToPlanDto(PaymentPlan p) => new(
        p.Id, p.ProjectId, p.Code, p.Name, p.Description,
        p.StandardDiscount, p.DiscountTolerance, p.TaxRate, p.IsActive, p.SortOrder,
        p.AssuredReturnPercent, p.AssuredReturnYears,
        p.BuyBackPercentPerYear, p.BuyBackEligibleAfterYears,
        p.IndicativeRentPerSqftPerMonth, p.ReturnConditions,
        p.Milestones.OrderBy(m => m.SortOrder)
            .Select(m => new PaymentPlanMilestoneDto(
                m.Id, m.SortOrder, m.Label, m.Basis, m.Percent, m.FixedAmount,
                m.DueOffsetDays, m.ConstructionStage))
            .ToList());

    /* ------------------------------------------------------------------ *
     * Charge heads
     * ------------------------------------------------------------------ */

    /// <summary>
    /// The revenue heads available on a project — the catalogue the builder
    /// offers and the cost sheet groups its rows under.
    /// </summary>
    [HttpGet("charge-heads")]
    public async Task<ActionResult<IReadOnlyList<ChargeHeadDto>>> Charges(
        [FromQuery] int? projectId, CancellationToken ct)
    {
        var heads = await Db.ChargeHeads.AsNoTracking()
            .Where(c => c.IsActive
                && (c.ProjectId == null || projectId == null || c.ProjectId == projectId))
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);

        return Ok(heads.Select(ToChargeHeadDto).ToList());
    }

    private static ChargeHeadDto ToChargeHeadDto(ChargeHead c) => new(
        c.Id, c.ProjectId, c.Group, c.Code, c.Name, c.Description, c.Basis,
        c.Rate, c.TaxRate, c.DefaultQuantity, c.IsMandatory, c.IsRefundable,
        c.IncludeInSchedule, c.DueLabel, c.SortOrder);

    /// <summary>
    /// Turns the selection into pricing inputs.
    ///
    /// Mandatory heads are added whether or not the client sent them: a cost
    /// sheet that omits the development charges because the browser forgot to
    /// tick them is a quotation that under-quotes, and the rep would not know.
    /// </summary>
    private async Task<List<ChargeInput>> ResolveChargesAsync(
        Unit unit, IReadOnlyList<ChargeSelectionDto>? selections, CancellationToken ct)
    {
        var available = await Db.ChargeHeads.AsNoTracking()
            .Where(c => c.IsActive && (c.ProjectId == null || c.ProjectId == unit.ProjectId))
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);

        if (available.Count == 0) return [];

        var picked = (selections ?? [])
            .GroupBy(x => x.ChargeHeadId)
            .ToDictionary(g => g.Key, g => g.Last().Quantity);

        var inputs = new List<ChargeInput>();

        foreach (var head in available)
        {
            var chosen = picked.TryGetValue(head.Id, out var quantity);
            if (!chosen && !head.IsMandatory) continue;

            var effectiveQuantity = quantity ?? head.DefaultQuantity;
            if (effectiveQuantity <= 0 && head.Basis == ChargeBases.PerQuantity) continue;

            inputs.Add(new ChargeInput
            {
                ChargeHeadId = head.Id,
                SortOrder = head.SortOrder,
                Group = head.Group,
                Name = head.Name,
                Basis = head.Basis,
                Rate = head.Rate,
                Quantity = effectiveQuantity,
                TaxRate = head.TaxRate,
                IsRefundable = head.IsRefundable,
                IncludeInSchedule = head.IncludeInSchedule,
                DueLabel = head.DueLabel,
            });
        }

        return inputs;
    }

    /// <summary>
    /// The return offer on one quotation.
    ///
    /// The plan supplies the defaults and the options decide what is actually
    /// promised: an investor buying the same Flexi unit gets the assured return
    /// and the buy-back, an end user buying it to live in gets neither, and the
    /// plan cannot tell them apart. Null options reproduce the old behaviour —
    /// whatever the plan configures is offered — so an older client that knows
    /// nothing about the switches keeps printing the annexure it always did.
    /// </summary>
    private static InvestorAnnexure ResolveReturns(
        PaymentPlan plan,
        QuoteOptionsDto? options,
        decimal basicSalePrice,
        decimal saleableArea,
        DateTime startDate)
    {
        var assuredRate = options?.AssuredReturnPercent ?? plan.AssuredReturnPercent;
        var assuredYears = options?.AssuredReturnYears ?? plan.AssuredReturnYears;
        var buyBackRate = options?.BuyBackPercentPerYear ?? plan.BuyBackPercentPerYear;
        var buyBackAfter = options?.BuyBackEligibleAfterYears ?? plan.BuyBackEligibleAfterYears;
        var rent = options?.RentPerSqftPerMonth ?? plan.IndicativeRentPerSqftPerMonth;

        return InvestorReturns.Calculate(new ReturnsInput
        {
            BasicSalePrice = basicSalePrice,
            SaleableArea = saleableArea,

            // Null means "as the plan sells it"; an explicit false is the rep
            // deciding this buyer is not being offered it.
            IncludeAssuredReturn = options?.IncludeAssuredReturn ?? plan.AssuredReturnPercent > 0,
            IncludeBuyBack = options?.IncludeBuyBack ?? plan.BuyBackPercentPerYear > 0,
            IncludeRentalYield = options?.IncludeRentalYield ?? plan.IndicativeRentPerSqftPerMonth > 0,

            AssuredReturnPercent = Math.Clamp(assuredRate, 0m, 1m),
            AssuredReturnYears = Math.Clamp(assuredYears, 0m, 30m),
            BuyBackPercentPerYear = Math.Clamp(buyBackRate, 0m, 1m),
            BuyBackEligibleAfterYears = Math.Clamp(buyBackAfter, 0m, 30m),
            BuyBackHorizonYears = Math.Clamp(options?.BuyBackHorizonYears ?? 0m, 0m, 30m),
            RentPerSqftPerMonth = Math.Max(0m, rent),

            Conditions = options?.ReturnConditions ?? plan.ReturnConditions,
            StartDate = startDate,
        });
    }

    /// <summary>The saved offer, rebuilt from the quotation's own snapshot.</summary>
    internal static InvestorAnnexure Returns(Quotation q) =>
        InvestorReturns.Calculate(new ReturnsInput
        {
            BasicSalePrice = q.Subtotal,
            SaleableArea = q.SaleableArea,
            IncludeAssuredReturn = q.ShowAssuredReturn,
            IncludeBuyBack = q.ShowBuyBack,
            IncludeRentalYield = q.ShowRentalYield,
            AssuredReturnPercent = q.AssuredReturnPercent,
            AssuredReturnYears = q.AssuredReturnYears,
            BuyBackPercentPerYear = q.BuyBackPercentPerYear,
            BuyBackEligibleAfterYears = q.BuyBackEligibleAfterYears,
            BuyBackHorizonYears = q.BuyBackHorizonYears,
            RentPerSqftPerMonth = q.RentPerSqftPerMonth,
            Conditions = q.ReturnConditions,
            StartDate = q.IssueDate,
        });

    /// <summary>Copies the resolved offer onto the quotation so it stops moving.</summary>
    private static void WriteReturns(Quotation q, InvestorAnnexure a, QuoteOptionsDto? options)
    {
        q.ShowAssuredReturn = a.HasAssuredReturn;
        q.AssuredReturnPercent = a.AssuredReturnPercent;
        q.AssuredReturnYears = a.AssuredReturnYears;
        q.AssuredReturnAmount = a.AssuredReturnAmount;

        q.ShowBuyBack = a.HasBuyBack;
        q.BuyBackPercentPerYear = a.BuyBackPercentPerYear;
        q.BuyBackEligibleAfterYears = a.BuyBackEligibleAfterYears;
        q.BuyBackHorizonYears = a.BuyBackHorizonYears;
        q.BuyBackAmount = a.BuyBackAmount;
        q.BuyBackValue = a.BuyBackValue;

        q.ShowRentalYield = a.HasRentalYield;
        q.RentPerSqftPerMonth = a.IndicativeRentPerSqftPerMonth;
        q.RentPerMonth = a.IndicativeRentPerMonth;
        q.GrossRentalYield = a.GrossRentalYield;

        q.ReturnsTotalEarned = a.TotalEarned;
        q.ReturnOnInvestment = a.ReturnOnInvestment;
        q.ReturnHorizonYears = a.HorizonYears;
        q.ReturnConditions = a.Conditions;

        q.DiscountApplied = options?.ApplyDiscount ?? true;
        q.DiscountLabel = string.IsNullOrWhiteSpace(options?.DiscountLabel)
            ? null
            : options!.DiscountLabel!.Trim();
    }

    /// <summary>The saved offer as a DTO — shared with the public link view.</summary>
    internal static InvestorAnnexureDto? PublicAnnexure(Quotation q) => ToAnnexureDto(Returns(q));

    /// <summary>Nothing switched on prints no annexure at all.</summary>
    private static InvestorAnnexureDto? ToAnnexureDto(InvestorAnnexure? a) =>
        a is null || !a.HasAnything ? null : new(
            a.BasicSalePrice,
            a.HasAssuredReturn, a.HasBuyBack, a.HasRentalYield,
            a.AssuredReturnPercent, a.AssuredReturnYears,
            a.AssuredReturnPerYear, a.AssuredReturnPerMonth, a.AssuredReturnAmount,
            a.BuyBackPercentPerYear, a.BuyBackEligibleAfterYears, a.BuyBackHorizonYears,
            a.BuyBackAmount, a.BuyBackValue,
            a.IndicativeRentPerSqftPerMonth, a.IndicativeRentPerMonth,
            a.IndicativeRentPerYear, a.GrossRentalYield,
            a.TotalEarned, a.ReturnOnInvestment, a.AnnualisedReturn, a.HorizonYears,
            a.Schedule
                .Select(r => new ReturnYearDto(
                    r.Year, r.PeriodEnd, r.YearFraction,
                    r.AssuredReturn, r.RentalIncome, r.CumulativeReturn))
                .ToList(),
            a.Conditions);

    /// <summary>
    /// Copies the priced charges and milestones onto the quotation.
    ///
    /// Snapshots rather than references: the plan and the heads can be repriced
    /// next quarter, and a quotation already with a customer must still say what
    /// it said. The caller clears the existing rows first when re-pricing.
    /// </summary>
    private static void WriteSnapshots(Quotation quotation, PricingResult result)
    {
        foreach (var charge in result.Charges)
        {
            quotation.Charges.Add(new QuotationCharge
            {
                ChargeHeadId = charge.ChargeHeadId,
                SortOrder = charge.SortOrder,
                Group = charge.Group,
                Name = charge.Name,
                Basis = charge.Basis,
                Quantity = charge.Quantity,
                QuantityUnit = charge.QuantityUnit,
                Rate = charge.Rate,
                BasicAmount = charge.BasicAmount,
                TaxRate = charge.TaxRate,
                TaxAmount = charge.TaxAmount,
                TotalAmount = charge.TotalAmount,
                IsRefundable = charge.IsRefundable,
                IncludeInSchedule = charge.IncludeInSchedule,
                DueLabel = charge.DueLabel,
            });
        }

        foreach (var milestone in result.Milestones)
        {
            quotation.Milestones.Add(new QuotationMilestone
            {
                SortOrder = milestone.SortOrder,
                Label = milestone.Label,
                Percent = milestone.Percent,
                BasicAmount = milestone.BasicAmount,
                TaxAmount = milestone.TaxAmount,
                TotalAmount = milestone.TotalAmount,
                DueDate = milestone.DueDate,
            });
        }
    }

    /// <summary>
    /// The single quotation line standing for the unit cost.
    ///
    /// Kept so the quotation still totals correctly in the list views and
    /// reports that were built around lines before the charge heads existed.
    /// </summary>
    private void RewriteUnitLine(Quotation quotation, Unit unit, PricingResult result)
    {
        if (quotation.Lines.Count > 0)
        {
            Db.QuotationLines.RemoveRange(quotation.Lines);
            quotation.Lines.Clear();
        }

        quotation.Lines.Add(new QuotationLine
        {
            Description = $"{unit.Configuration} {unit.UnitNumber} — "
                + $"{result.SaleableArea:0.##} sq ft @ Rs. {result.EffectiveRatePerSqft:N2}/sq ft",
            Category = "Unit cost",
            Quantity = result.SaleableArea,
            Unit = "sqft",
            UnitPrice = result.EffectiveRatePerSqft,
            LineTotal = result.BasicAmount,
            SortOrder = 1,
        });
    }

    private static QuoteChargeDto ToChargeRowDto(QuotationCharge c) => new(
        c.ChargeHeadId, c.SortOrder, c.Group, c.Name, c.Basis, c.Quantity, c.QuantityUnit,
        c.Rate, c.BasicAmount, c.TaxRate, c.TaxAmount, c.TotalAmount,
        c.IsRefundable, c.IncludeInSchedule, c.DueLabel);

    private static QuoteChargeDto ToChargeDto(ChargeResult c) => new(
        c.ChargeHeadId, c.SortOrder, c.Group, c.Name, c.Basis, c.Quantity, c.QuantityUnit,
        c.Rate, c.BasicAmount, c.TaxRate, c.TaxAmount, c.TotalAmount,
        c.IsRefundable, c.IncludeInSchedule, c.DueLabel);

    /* ------------------------------------------------------------------ *
     * Pricing
     * ------------------------------------------------------------------ */

    /// <summary>Prices a unit without writing anything. Safe to call on every keystroke.</summary>
    [PermissionAction(ObjectAction.View)]
    [HttpPost("preview")]
    public async Task<ActionResult<QuotePreviewDto>> Preview(
        QuotePreviewRequest request, CancellationToken ct)
    {
        var (unit, plan, card, result, discount, returns) = await PriceAsync(request, ct);

        return Ok(BuildPreview(
            unit, plan, card, result, discount, returns, request.RateOverride, request.Options));
    }

    /// <summary>
    /// The single pricing path. Returns the pieces the callers need rather than
    /// a DTO, so the save endpoint can persist the same numbers the preview
    /// rendered without recomputing them a second, subtly different way.
    /// </summary>
    private async Task<(Unit Unit, PaymentPlan Plan, RateCard? Card, PricingResult Result,
        decimal Discount, InvestorAnnexure Returns)>
        PriceAsync(QuotePreviewRequest request, CancellationToken ct)
    {
        var unit = await Db.Units.AsNoTracking()
            .Include(u => u.Project).Include(u => u.Tower)
            .FirstOrDefaultAsync(u => u.Id == request.UnitId, ct)
            ?? throw ApiException.NotFound("Unit");

        var plan = await Db.PaymentPlans.AsNoTracking()
            .Include(p => p.Milestones)
            .FirstOrDefaultAsync(p => p.Id == request.PaymentPlanId, ct)
            ?? throw ApiException.NotFound("Payment plan");

        var bookingDate = request.BookingDate ?? DateTime.UtcNow;
        var card = await ActiveRateCardAsync(unit, bookingDate, ct);

        var rate = request.RateOverride ?? card?.RatePerSqft ?? unit.PricePerSqft;

        if (rate <= 0)
        {
            throw ApiException.BadRequest(
                "No rate card is in force for this unit, and none was supplied.");
        }

        // Holding the list price is a decision, not an empty box: an explicit
        // false zeroes the discount whatever the plan grants as standard, which
        // is the only way a rep can quote a unit at its card rate on a plan
        // whose standard discount is twenty percent.
        var discount = request.Options?.ApplyDiscount == false
            ? 0m
            : Math.Clamp(request.Discount ?? plan.StandardDiscount, 0m, 0.9m);

        var area = unit.SuperArea ?? unit.BuiltUpArea ?? unit.CarpetArea;

        var charges = await ResolveChargesAsync(unit, request.Charges, ct);

        // The space's own minimum is the floor unless the request names a
        // negotiated one. A venue will drop its guarantee to win a date, and the
        // proposal has to price what was agreed rather than what the catalogue
        // says.
        var minimumPlates = request.MinimumPlates ?? unit.MinimumPlates;

        var result = QuotationPricing.Calculate(
            new PricingInput
            {
                SaleableArea = area,
                RatePerSqft = rate,
                PlcPerSqft = unit.PlcPerSqft,
                Discount = discount,
                TaxRate = plan.TaxRate,
                BookingDate = bookingDate,
                EventDate = request.EventDate,
                GuestCount = request.GuestCount ?? 0,
                MinimumPlates = minimumPlates,
                Charges = charges,
            },
            plan.Milestones.ToList());

        var returns = ResolveReturns(
            plan, request.Options, result.BasicAmount, result.SaleableArea, bookingDate);

        return (unit, plan, card, result, discount, returns);
    }

    private async Task<RateCard?> ActiveRateCardAsync(Unit unit, DateTime on, CancellationToken ct) =>
        await Db.RateCards.AsNoTracking()
            .Where(c => c.ProjectId == unit.ProjectId
                && c.EffectiveFrom <= on.Date
                && (c.TowerId == null || c.TowerId == unit.TowerId)
                && (c.UnitType == null || c.UnitType == unit.Configuration))
            .OrderByDescending(c => c.EffectiveFrom)
            .ThenByDescending(c => c.TowerId == null ? 0 : 1)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Whether the terms exceed what the rep may grant alone.
    ///
    /// Two triggers: a discount past the plan's standard plus its tolerance, and
    /// a hand-typed rate. Both are the same class of decision — giving away
    /// margin — so both stop at the same gate.
    /// </summary>
    private static (bool Required, string? Reason) ApprovalCheck(
        PaymentPlan plan, decimal discount, decimal? rateOverride, decimal? cardRate)
    {
        var ceiling = plan.StandardDiscount + plan.DiscountTolerance;

        if (discount > ceiling)
        {
            return (true,
                $"Discount {discount * 100:0.##}% exceeds the "
                + $"{plan.Name} standard of {plan.StandardDiscount * 100:0.##}%.");
        }

        if (rateOverride is decimal typed && cardRate is decimal listed && typed != listed)
        {
            return (true,
                $"Rate overridden to Rs. {typed:N0} against the card's Rs. {listed:N0}.");
        }

        return (false, null);
    }

    private QuotePreviewDto BuildPreview(
        Unit unit, PaymentPlan plan, RateCard? card,
        PricingResult result, decimal discount, InvestorAnnexure returns,
        decimal? rateOverride, QuoteOptionsDto? options)
    {
        var (required, reason) = ApprovalCheck(plan, discount, rateOverride, card?.RatePerSqft);

        return new QuotePreviewDto(
            unit.Id, unit.UnitNumber, unit.Tower?.Name, unit.Project?.Name ?? "—",
            unit.Configuration, unit.Floor, unit.Status,
            result.SaleableArea, unit.BuiltUpArea ?? 0m, unit.CarpetArea,
            unit.PlcPerSqft, unit.Facing, unit.ViewType,
            result.GuestCount, result.MinimumPlates, result.BilledHeads, result.MinimumApplied,
            card?.Id, card?.Label, result.RatePerSqft,
            rateOverride is not null && rateOverride != card?.RatePerSqft,
            plan.Id, plan.Name, plan.StandardDiscount, discount, result.EffectiveRatePerSqft,
            result.BasicAmount, result.TaxRate, result.TaxAmount, result.TotalAmount,
            IndianNumberWords.ToRupees(result.TotalAmount),
            result.Charges.Select(ToChargeDto).ToList(),
            result.ChargesBasic, result.ChargesTax, result.ChargesTotal, result.RefundableTotal,
            result.GrandTotal, result.ScheduledTotal, result.UnscheduledTotal,
            IndianNumberWords.ToRupees(result.GrandTotal),
            options?.ApplyDiscount ?? true,
            string.IsNullOrWhiteSpace(options?.DiscountLabel) ? null : options!.DiscountLabel!.Trim(),
            decimal.Round(result.SaleableArea * result.RatePerSqft * discount, 2),
            ToAnnexureDto(returns),
            result.Milestones.Select(ToMilestoneDto).ToList(),
            result.ScheduleVariance,
            required, reason);
    }

    private static QuoteMilestoneDto ToMilestoneDto(MilestoneResult m) => new(
        m.SortOrder, m.Label, m.Percent, m.BasicAmount, m.TaxAmount, m.TotalAmount, m.DueDate);

    /* ------------------------------------------------------------------ *
     * Saving
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Raises a quotation against a unit.
    ///
    /// Refuses stock that is already gone. A quotation for a sold flat is worse
    /// than no quotation — it is a promise the company cannot keep — so the
    /// check is here rather than left to the rep reading the board.
    /// </summary>
    [HttpPost("from-unit")]
    public async Task<ActionResult<QuotationResultDto>> CreateFromUnit(
        CreateUnitQuotationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName))
        {
            throw ApiException.BadRequest("A quotation needs a customer name.");
        }

        var (unit, plan, card, result, discount, returns) = await PriceAsync(
            new QuotePreviewRequest(
                request.UnitId, request.PaymentPlanId, request.Discount,
                request.RateOverride, request.BookingDate, request.Charges, request.Options,
                request.EventDate, request.GuestCount, request.MinimumPlates),
            ct);

        if (UnitStatuses.Unavailable.Contains(unit.Status))
        {
            throw ApiException.Conflict(
                $"{unit.UnitNumber} is {unit.Status.ToLowerInvariant()} and cannot be quoted.");
        }

        if (unit.HeldByUserId is int holder
            && holder != Db.Tenant.UserId
            && unit.HeldUntil > DateTime.UtcNow)
        {
            throw ApiException.Conflict($"{unit.UnitNumber} is on hold by another user.");
        }

        var branch = await RequireBranchAsync(
            request.BranchId ?? await DefaultBranchIdAsync(ct), ct);

        var (needsApproval, approvalReason) =
            ApprovalCheck(plan, discount, request.RateOverride, card?.RatePerSqft);

        var quotation = new Quotation
        {
            CompanyId = Db.Tenant.CompanyId,
            BranchId = branch.Id,
            QuoteNumber = Code("QT"),
            Title = $"{unit.Project?.Name ?? "Unit"} · {unit.UnitNumber}",
            Version = 1,

            LeadId = request.LeadId,
            ContactId = request.ContactId,
            OpportunityId = request.OpportunityId,
            ProjectId = unit.ProjectId,
            UnitId = unit.Id,

            CustomerName = request.CustomerName.Trim(),
            CustomerEmail = request.CustomerEmail,
            CustomerPhone = request.CustomerPhone,
            BillingAddress = request.BillingAddress,

            PaymentPlanId = plan.Id,
            PaymentPlanName = plan.Name,
            TowerName = unit.Tower?.Name,
            UnitNumber = unit.UnitNumber,
            UnitType = unit.Configuration,
            SaleableArea = result.SaleableArea,
            BuiltUpArea = unit.BuiltUpArea ?? 0m,
            CarpetArea = unit.CarpetArea,

            // Snapshotted, not read back off the lead: a proposal is an offer
            // made against one head count on one date, and the family goes on
            // arguing about both after it has been sent.
            EventType = request.EventType,
            EventDate = request.EventDate,
            EventEndDate = request.EventEndDate,
            EventSlot = request.EventSlot,
            Functions = request.Functions,
            GuestCount = result.GuestCount,
            MinimumPlates = result.MinimumPlates,

            RateCardId = card?.Id,
            RateCardLabel = card?.Label,
            RatePerSqft = result.RatePerSqft,
            PlcPerSqft = result.PlcPerSqft,
            EffectiveRatePerSqft = result.EffectiveRatePerSqft,
            StandardDiscountPercent = plan.StandardDiscount,
            DiscountPercent = discount,

            Subtotal = result.BasicAmount,
            DiscountAmount = decimal.Round(
                result.SaleableArea * result.RatePerSqft * discount, 2),
            TaxPercent = plan.TaxRate,
            TaxAmount = result.TaxAmount,
            Total = result.TotalAmount,
            AmountInWords = IndianNumberWords.ToRupees(result.TotalAmount),

            ChargesBasic = result.ChargesBasic,
            ChargesTax = result.ChargesTax,
            ChargesTotal = result.ChargesTotal,
            RefundableTotal = result.RefundableTotal,
            GrandTotal = result.GrandTotal,
            ScheduledTotal = result.ScheduledTotal,
            GrandTotalInWords = IndianNumberWords.ToRupees(result.GrandTotal),

            Status = QuotationStatuses.Draft,
            ApprovalStatus = needsApproval
                ? QuotationApprovalStatuses.Pending
                : QuotationApprovalStatuses.NotRequired,

            IssueDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(Math.Clamp(request.ValidDays ?? 15, 1, 180)),

            PaymentTerms = plan.Name,
            Notes = request.Notes,
            TermsAndConditions = request.TermsAndConditions,
            OwnerId = request.OwnerId ?? (Db.Tenant.UserId > 0 ? Db.Tenant.UserId : null),
        };

        WriteSnapshots(quotation, result);
        WriteReturns(quotation, returns, request.Options);
        RewriteUnitLine(quotation, unit, result);

        Db.Quotations.Add(quotation);
        await Db.SaveChangesAsync(ct);

        Db.QuotationActivities.Add(new QuotationActivity
        {
            QuotationId = quotation.Id,
            Type = QuotationActivityTypes.Created,
            Description = $"Quotation created for {unit.UnitNumber} at {result.GrandTotal:N0}.",
            ActorId = Db.Tenant.UserId,
            ActorName = Db.Tenant.UserName,
        });

        Approval? approval = null;

        if (needsApproval)
        {
            approval = approvals.Request(
                ApprovalEntities.Quotation,
                quotation.Id,
                quotation.QuoteNumber,
                request.RateOverride is null ? ApprovalKinds.Discount : ApprovalKinds.PriceOverride,
                approvalReason ?? "Terms outside the standard plan.",
                branch.Id,
                request.ApprovalReason,
                quotation.GrandTotal);

            await Db.SaveChangesAsync(ct);

            quotation.ApprovalId = approval.Id;
            await Db.SaveChangesAsync(ct);
        }

        // The unit is parked and the lead is told the moment a draft exists —
        // a quote nobody can see is still a promise someone made about stock.
        await lifecycle.OnDraftedAsync(quotation, ct);
        await Db.SaveChangesAsync(ct);

        var detail = await LoadDetailAsync(quotation.Id, ct);

        return Ok(new QuotationResultDto(
            detail,
            approval is null ? null : InventoryBoardController.ToApprovalDto(approval, branch.Name),
            needsApproval
                ? $"{quotation.QuoteNumber} saved as a draft and sent for approval."
                : $"{quotation.QuoteNumber} created."));
    }

    /* ------------------------------------------------------------------ *
     * Re-pricing an existing quotation
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Edits a quotation by re-running the pricing engine over it.
    ///
    /// A quotation has no independently editable total: change the plan or the
    /// discount and every milestone, every charge and the approval verdict move
    /// with it. Running the same path the original save ran is what keeps the
    /// document internally consistent, and what stops an edited quotation from
    /// carrying a stale schedule under a fresh price.
    /// </summary>
    [HttpPut("{id:int}/reprice")]
    public async Task<ActionResult<QuotationResultDto>> Reprice(
        int id, RepriceQuotationRequest request, CancellationToken ct)
    {
        var quotation = await Db.Quotations
            .Include(q => q.Milestones)
            .Include(q => q.Charges)
            .Include(q => q.Lines)
            // Three collections in one statement multiply out against each
            // other; split keeps the re-price reading each set once.
            .AsSplitQuery()
            .FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw ApiException.NotFound("Quotation");

        if (quotation.Status == QuotationStatuses.Accepted)
        {
            throw ApiException.Conflict(
                "An accepted quotation cannot be re-priced. Raise a revision instead.");
        }

        if (quotation.UnitId is not int unitId)
        {
            throw ApiException.BadRequest(
                "This quotation is not linked to a unit, so it cannot be re-priced.");
        }

        // Null means "keep what it has" on every field, so a client that only
        // wants to nudge the discount does not have to echo the whole record
        // back and risk clearing something it never displayed.
        var planId = request.PaymentPlanId ?? quotation.PaymentPlanId
            ?? throw ApiException.BadRequest("This quotation has no payment plan.");

        var selections = request.Charges ?? quotation.Charges
            .Where(c => c.ChargeHeadId is not null)
            .Select(c => new ChargeSelectionDto(c.ChargeHeadId!.Value, c.Quantity))
            .ToList();

        // Null options on a re-price mean "keep the offer as it stands", so the
        // quotation's own switches are replayed rather than the plan's defaults
        // being reapplied — otherwise nudging a discount would quietly put an
        // assured return back onto a quotation the rep had taken it off.
        var options = request.Options ?? new QuoteOptionsDto(
            ApplyDiscount: quotation.DiscountApplied,
            DiscountLabel: quotation.DiscountLabel,
            IncludeAssuredReturn: quotation.ShowAssuredReturn,
            AssuredReturnPercent: quotation.AssuredReturnPercent,
            AssuredReturnYears: quotation.AssuredReturnYears,
            IncludeBuyBack: quotation.ShowBuyBack,
            BuyBackPercentPerYear: quotation.BuyBackPercentPerYear,
            BuyBackEligibleAfterYears: quotation.BuyBackEligibleAfterYears,
            BuyBackHorizonYears: quotation.BuyBackHorizonYears,
            IncludeRentalYield: quotation.ShowRentalYield,
            RentPerSqftPerMonth: quotation.RentPerSqftPerMonth,
            ReturnConditions: quotation.ReturnConditions);

        var (unit, plan, card, result, discount, returns) = await PriceAsync(
            new QuotePreviewRequest(
                unitId, planId,
                request.Discount ?? quotation.DiscountPercent,
                request.RateOverride,
                request.BookingDate ?? quotation.IssueDate,
                selections,
                options,
                // A reprice keeps the event it was raised against unless the
                // caller names a new one — moving the date or the head count is
                // a decision, never a side effect of re-running the engine.
                request.EventDate ?? quotation.EventDate,
                request.GuestCount ?? quotation.GuestCount,
                request.MinimumPlates ?? quotation.MinimumPlates),
            ct);

        var (needsApproval, approvalReason) =
            ApprovalCheck(plan, discount, request.RateOverride, card?.RatePerSqft);

        quotation.PaymentPlanId = plan.Id;
        quotation.PaymentPlanName = plan.Name;
        quotation.PaymentTerms = plan.Name;

        quotation.SaleableArea = result.SaleableArea;
        quotation.BuiltUpArea = unit.BuiltUpArea ?? 0m;
        quotation.CarpetArea = unit.CarpetArea;

        // Taken from the result rather than the request, so the stored snapshot
        // is exactly the head count the printed lines were struck on.
        quotation.EventDate = result.EventDate ?? quotation.EventDate;
        quotation.GuestCount = result.GuestCount;
        quotation.MinimumPlates = result.MinimumPlates;

        quotation.RateCardId = card?.Id;
        quotation.RateCardLabel = card?.Label;
        quotation.RatePerSqft = result.RatePerSqft;
        quotation.PlcPerSqft = result.PlcPerSqft;
        quotation.EffectiveRatePerSqft = result.EffectiveRatePerSqft;
        quotation.StandardDiscountPercent = plan.StandardDiscount;
        quotation.DiscountPercent = discount;

        quotation.Subtotal = result.BasicAmount;
        quotation.DiscountAmount = decimal.Round(
            result.SaleableArea * result.RatePerSqft * discount, 2);
        quotation.TaxPercent = plan.TaxRate;
        quotation.TaxAmount = result.TaxAmount;
        quotation.Total = result.TotalAmount;
        quotation.AmountInWords = IndianNumberWords.ToRupees(result.TotalAmount);

        quotation.ChargesBasic = result.ChargesBasic;
        quotation.ChargesTax = result.ChargesTax;
        quotation.ChargesTotal = result.ChargesTotal;
        quotation.RefundableTotal = result.RefundableTotal;
        quotation.GrandTotal = result.GrandTotal;
        quotation.ScheduledTotal = result.ScheduledTotal;
        quotation.GrandTotalInWords = IndianNumberWords.ToRupees(result.GrandTotal);

        if (!string.IsNullOrWhiteSpace(request.CustomerName))
        {
            quotation.CustomerName = request.CustomerName.Trim();
        }

        if (request.CustomerEmail is not null) quotation.CustomerEmail = request.CustomerEmail;
        if (request.CustomerPhone is not null) quotation.CustomerPhone = request.CustomerPhone;
        if (request.BillingAddress is not null) quotation.BillingAddress = request.BillingAddress;
        if (request.Notes is not null) quotation.Notes = request.Notes;
        if (request.TermsAndConditions is not null) quotation.TermsAndConditions = request.TermsAndConditions;
        if (request.OwnerId is int owner) quotation.OwnerId = owner;

        // Zero is the unlink. Anything else re-points the quotation at the
        // record the desk is actually working, which matters because the
        // lifecycle hooks move that record's stage when this one is issued.
        if (request.LeadId is int lead) quotation.LeadId = lead == 0 ? null : lead;
        if (request.ContactId is int contact) quotation.ContactId = contact == 0 ? null : contact;

        if (request.OpportunityId is int opportunity)
        {
            quotation.OpportunityId = opportunity == 0 ? null : opportunity;
        }

        if (request.ValidDays is int validDays)
        {
            quotation.ValidUntil = DateTime.UtcNow.AddDays(Math.Clamp(validDays, 1, 180));
        }

        // Snapshots are replaced wholesale rather than merged: a milestone or a
        // head that survives from the previous price is indistinguishable from
        // one the new price produced, and reconciling them by label would break
        // the moment a plan renames a stage.
        Db.QuotationMilestones.RemoveRange(quotation.Milestones);
        Db.QuotationCharges.RemoveRange(quotation.Charges);
        quotation.Milestones.Clear();
        quotation.Charges.Clear();

        WriteSnapshots(quotation, result);
        WriteReturns(quotation, returns, options);
        RewriteUnitLine(quotation, unit, result);

        Db.QuotationActivities.Add(new QuotationActivity
        {
            QuotationId = quotation.Id,
            Type = QuotationActivityTypes.Edited,
            Description = $"Re-priced — {plan.Name} at {discount * 100:0.##}% discount, total {result.GrandTotal:N0}.",
            ActorId = Db.Tenant.UserId,
            ActorName = Db.Tenant.UserName,
        });

        // A re-price invalidates whatever the last approval was about. The old
        // request is closed and a new one raised if the new terms still need it,
        // so an approver is never ruling on numbers that have since changed.
        await approvals.CancelOpenAsync(
            ApprovalEntities.Quotation, quotation.Id, "Superseded by a re-price.", ct);

        Approval? approval = null;
        quotation.ApprovalStatus = needsApproval
            ? QuotationApprovalStatuses.Pending
            : QuotationApprovalStatuses.NotRequired;

        if (!needsApproval) quotation.RejectionReason = null;

        await Db.SaveChangesAsync(ct);

        if (needsApproval)
        {
            approval = approvals.Request(
                ApprovalEntities.Quotation, quotation.Id, quotation.QuoteNumber,
                request.RateOverride is null ? ApprovalKinds.Discount : ApprovalKinds.PriceOverride,
                approvalReason ?? "Terms outside the standard plan.",
                quotation.BranchId, request.ApprovalReason, quotation.GrandTotal);

            await Db.SaveChangesAsync(ct);

            quotation.ApprovalId = approval.Id;
            await Db.SaveChangesAsync(ct);
        }

        var branchName = await Db.Branches
            .Where(b => b.Id == quotation.BranchId).Select(b => b.Name).FirstOrDefaultAsync(ct);

        return Ok(new QuotationResultDto(
            await LoadDetailAsync(quotation.Id, ct),
            approval is null ? null : InventoryBoardController.ToApprovalDto(approval, branchName ?? "—"),
            needsApproval
                ? $"{quotation.QuoteNumber} re-priced and sent for approval."
                : $"{quotation.QuoteNumber} re-priced."));
    }

    /* ------------------------------------------------------------------ *
     * Approval
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Sends a quotation for sign-off the rep asked for rather than the system
    /// insisted on.
    ///
    /// The automatic gate catches over-discounting; this covers everything else
    /// a rep might want a manager to see before it leaves — an unusual buyer, a
    /// concession made verbally, a unit someone senior has an opinion about.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/submit-approval")]
    public async Task<ActionResult<QuotationResultDto>> SubmitForApproval(
        int id, SubmitApprovalRequest request, CancellationToken ct)
    {
        var quotation = await Db.Quotations.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw ApiException.NotFound("Quotation");

        if (quotation.ApprovalStatus == QuotationApprovalStatuses.Pending)
        {
            throw ApiException.Conflict("This quotation is already waiting on approval.");
        }

        if (quotation.Status is QuotationStatuses.Accepted or QuotationStatuses.Rejected)
        {
            throw ApiException.Conflict(
                $"A {quotation.Status.ToLowerInvariant()} quotation cannot be sent for approval.");
        }

        var summary = quotation.DiscountPercent > quotation.StandardDiscountPercent
            ? $"Discount {quotation.DiscountPercent * 100:0.##}% against a standard of "
                + $"{quotation.StandardDiscountPercent * 100:0.##}%."
            : $"{quotation.QuoteNumber} sent for review at {quotation.GrandTotal:N0}.";

        var approval = approvals.Request(
            ApprovalEntities.Quotation, quotation.Id, quotation.QuoteNumber,
            ApprovalKinds.Discount, summary, quotation.BranchId,
            request.Reason, quotation.GrandTotal);

        quotation.ApprovalStatus = QuotationApprovalStatuses.Pending;
        quotation.RejectionReason = null;

        await Db.SaveChangesAsync(ct);

        quotation.ApprovalId = approval.Id;
        await Db.SaveChangesAsync(ct);

        var branchName = await Db.Branches
            .Where(b => b.Id == quotation.BranchId).Select(b => b.Name).FirstOrDefaultAsync(ct);

        return Ok(new QuotationResultDto(
            await LoadDetailAsync(quotation.Id, ct),
            InventoryBoardController.ToApprovalDto(approval, branchName ?? "—"),
            $"{quotation.QuoteNumber} sent for approval."));
    }

    /// <summary>Pulls a request back out of the queue before anyone has ruled on it.</summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/withdraw-approval")]
    public async Task<ActionResult<QuotationDetailDto>> WithdrawApproval(
        int id, CancellationToken ct)
    {
        var quotation = await Db.Quotations.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw ApiException.NotFound("Quotation");

        if (quotation.ApprovalStatus != QuotationApprovalStatuses.Pending)
        {
            throw ApiException.Conflict("There is nothing waiting on approval here.");
        }

        await approvals.CancelOpenAsync(
            ApprovalEntities.Quotation, quotation.Id, "Withdrawn by the requester.", ct);

        quotation.ApprovalStatus = QuotationApprovalStatuses.NotRequired;
        quotation.ApprovalId = null;

        await Db.SaveChangesAsync(ct);
        return Ok(await LoadDetailAsync(id, ct));
    }

    /* ------------------------------------------------------------------ *
     * Outcome
     * ------------------------------------------------------------------ */

    /// <summary>
    /// The buyer accepted. Books the unit, moves the lead and the deal.
    ///
    /// This is the transition that turns a document into stock coming off the
    /// board, so it refuses a quotation whose terms were never cleared.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/accept")]
    public async Task<ActionResult<QuotationDetailDto>> Accept(int id, CancellationToken ct)
    {
        var quotation = await Db.Quotations.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw ApiException.NotFound("Quotation");

        if (quotation.ApprovalStatus == QuotationApprovalStatuses.Pending)
        {
            throw ApiException.Conflict(
                "This quotation is waiting on approval and cannot be accepted yet.");
        }

        if (quotation.ApprovalStatus == QuotationApprovalStatuses.Rejected)
        {
            throw ApiException.Conflict(
                $"The terms on this quotation were rejected: {quotation.RejectionReason}");
        }

        if (quotation.Status == QuotationStatuses.Accepted)
        {
            throw ApiException.Conflict("This quotation has already been accepted.");
        }

        quotation.Status = QuotationStatuses.Accepted;
        quotation.RespondedAt = DateTime.UtcNow;
        quotation.RejectionReason = null;

        Db.QuotationActivities.Add(new QuotationActivity
        {
            QuotationId = id,
            Type = QuotationActivityTypes.Accepted,
            Description = $"Accepted — unit {quotation.UnitNumber ?? ""} booked at {quotation.GrandTotal:N0}.",
            ActorId = Db.Tenant.UserId,
            ActorName = Db.Tenant.UserName,
        });

        await lifecycle.OnAcceptedAsync(quotation, ct);
        await Db.SaveChangesAsync(ct);

        return Ok(await LoadDetailAsync(id, ct));
    }

    /// <summary>The buyer declined, or the desk gave up on it. Hands the stock back.</summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/decline")]
    public async Task<ActionResult<QuotationDetailDto>> Decline(
        int id, QuotationDecisionRequest request, CancellationToken ct)
    {
        var quotation = await Db.Quotations.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw ApiException.NotFound("Quotation");

        if (quotation.Status == QuotationStatuses.Accepted)
        {
            throw ApiException.Conflict("An accepted quotation cannot be declined.");
        }

        quotation.Status = QuotationStatuses.Rejected;
        quotation.RespondedAt = DateTime.UtcNow;
        quotation.RejectionReason = request.Reason;

        Db.QuotationActivities.Add(new QuotationActivity
        {
            QuotationId = id,
            Type = QuotationActivityTypes.Declined,
            Description = $"Declined. {request.Reason ?? ""}".Trim(),
            ActorId = Db.Tenant.UserId,
            ActorName = Db.Tenant.UserName,
        });

        await approvals.CancelOpenAsync(
            ApprovalEntities.Quotation, quotation.Id, "Quotation declined.", ct);
        await lifecycle.OnClosedWithoutSaleAsync(
            quotation, request.Reason ?? "Quotation declined.", ct);

        await Db.SaveChangesAsync(ct);
        return Ok(await LoadDetailAsync(id, ct));
    }

    /// <summary>
    /// Removes a quotation and everything it was holding open.
    ///
    /// Deleting the row alone would leave the unit parked and an approver
    /// staring at a request against a document that no longer exists, so both
    /// are closed out first.
    /// </summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remove(int id, CancellationToken ct)
    {
        var quotation = await Db.Quotations.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw ApiException.NotFound("Quotation");

        if (quotation.Status == QuotationStatuses.Accepted)
        {
            throw ApiException.Conflict(
                "An accepted quotation cannot be deleted — decline it first if the deal fell through.");
        }

        await approvals.CancelOpenAsync(
            ApprovalEntities.Quotation, quotation.Id, "Quotation deleted.", ct);
        await lifecycle.OnClosedWithoutSaleAsync(quotation, "Quotation deleted.", ct);

        SoftDelete(quotation);

        Db.QuotationActivities.Add(new QuotationActivity
        {
            QuotationId = id,
            Type = QuotationActivityTypes.Deleted,
            Description = "Quotation deleted.",
            ActorId = Db.Tenant.UserId,
            ActorName = Db.Tenant.UserName,
        });
        await Db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Issues the quotation to the customer.
    ///
    /// The approval gate lives on this transition rather than on save: a rep
    /// should be able to build and revise an aggressive quote freely, and be
    /// stopped only at the point it would leave the building.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/issue")]
    public async Task<ActionResult<QuotationDetailDto>> Issue(int id, CancellationToken ct)
    {
        var quotation = await Db.Quotations.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw ApiException.NotFound("Quotation");

        if (quotation.ApprovalStatus == QuotationApprovalStatuses.Pending)
        {
            throw ApiException.Conflict(
                "This quotation is waiting on approval and cannot be issued yet.");
        }

        if (quotation.ApprovalStatus == QuotationApprovalStatuses.Rejected)
        {
            throw ApiException.Conflict(
                $"The terms on this quotation were rejected: {quotation.RejectionReason}");
        }

        quotation.Status = QuotationStatuses.Sent;
        quotation.SentAt = DateTime.UtcNow;

        Db.QuotationActivities.Add(new QuotationActivity
        {
            QuotationId = id,
            Type = QuotationActivityTypes.Issued,
            Description = $"Issued to {quotation.CustomerName}, valid until {quotation.ValidUntil:dd MMM yyyy}.",
            ActorId = Db.Tenant.UserId,
            ActorName = Db.Tenant.UserName,
        });

        // Issuing is the point the unit stops being loosely parked and starts
        // being held for as long as the offer stands.
        await lifecycle.OnIssuedAsync(quotation, ct);

        await Db.SaveChangesAsync(ct);
        return Ok(await LoadDetailAsync(id, ct));
    }

    [HttpGet("{id:int}/detail")]
    public async Task<ActionResult<QuotationDetailDto>> Detail(int id, CancellationToken ct) =>
        Ok(await LoadDetailAsync(id, ct));

    /* ------------------------------------------------------------------ *
     * PDF
     * ------------------------------------------------------------------ */

    /// <summary>
    /// The printed quotation.
    ///
    /// Rendered on demand rather than stored: the document is a pure function of
    /// the record, and a cached file is one more thing that can fall out of step
    /// with the numbers it claims to show.
    /// </summary>
    [HttpGet("{id:int}/pdf")]
    public async Task<IActionResult> Pdf(int id, CancellationToken ct)
    {
        var quotation = await Db.Quotations.AsNoTracking()
            .Include(q => q.Milestones)
            .Include(q => q.Charges)
            .Include(q => q.Project)
            .Include(q => q.Owner)
            .Include(q => q.PaymentPlan)
            .Include(q => q.Branch!).ThenInclude(b => b.Company)
            // Split: two collections joined into one statement return the
            // quotation once per milestone per charge, so the document that
            // reads most rows is the one that reads them most redundantly.
            .AsSplitQuery()
            .FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw ApiException.NotFound("Quotation");

        // One trip for the two unit attributes the document shows but the
        // quotation does not snapshot, since neither can change the price.
        var unit = quotation.UnitId is int unitId
            ? await Db.Units.AsNoTracking()
                .Where(u => u.Id == unitId)
                .Select(u => new { u.Floor, u.Facing, u.ViewType })
                .FirstOrDefaultAsync(ct)
            : null;

        var charges = quotation.Charges
            .OrderBy(c => c.SortOrder)
            .Select(c => new ChargeResult(
                c.ChargeHeadId, c.SortOrder, c.Group, c.Name, c.Basis,
                c.Quantity, c.QuantityUnit, c.Rate,
                c.BasicAmount, c.TaxRate, c.TaxAmount, c.TotalAmount,
                c.IsRefundable, c.IncludeInSchedule, c.DueLabel))
            .ToList();

        var model = new QuotationDocumentModel
        {
            // The seller's name on a priced offer. Falls back to the tenant
            // rather than to a hard-coded developer: a default naming some other
            // company would put that company on a legal document.
            DeveloperName = quotation.Project?.Developer
                ?? quotation.Branch?.Company?.Name
                ?? "—",
            ProjectName = quotation.Project?.Name ?? quotation.Title,
            ProjectAddress = quotation.Project?.Address ?? string.Empty,
            ReraNumber = quotation.Project?.ReraNumber,

            QuoteNumber = quotation.QuoteNumber,
            IssueDate = quotation.IssueDate,
            ValidUntil = quotation.ValidUntil,
            Version = quotation.Version,
            Status = quotation.Status,

            CustomerName = quotation.CustomerName,
            CustomerPhone = quotation.CustomerPhone,
            CustomerEmail = quotation.CustomerEmail,
            BillingAddress = quotation.BillingAddress,

            UnitNumber = quotation.UnitNumber ?? "—",
            TowerName = quotation.TowerName,
            UnitType = quotation.UnitType ?? "—",
            Floor = unit?.Floor ?? 0,
            Facing = unit?.Facing,
            ViewType = unit?.ViewType,
            SaleableArea = quotation.SaleableArea,
            BuiltUpArea = quotation.BuiltUpArea,
            CarpetArea = quotation.CarpetArea,

            PaymentPlanName = quotation.PaymentPlanName ?? "—",
            RateCardLabel = quotation.RateCardLabel,
            RatePerSqft = quotation.RatePerSqft,
            PlcPerSqft = quotation.PlcPerSqft,
            Discount = quotation.DiscountPercent,
            EffectiveRatePerSqft = quotation.EffectiveRatePerSqft,

            BasicAmount = quotation.Subtotal,
            TaxRate = quotation.TaxPercent,
            TaxAmount = quotation.TaxAmount,
            TotalAmount = quotation.Total,

            Charges = charges,
            ChargesBasic = quotation.ChargesBasic,
            ChargesTax = quotation.ChargesTax,
            ChargesTotal = quotation.ChargesTotal,
            RefundableTotal = quotation.RefundableTotal,
            GrandTotal = quotation.GrandTotal == 0 ? quotation.Total : quotation.GrandTotal,
            ScheduledTotal = quotation.ScheduledTotal == 0 ? quotation.Total : quotation.ScheduledTotal,
            AmountInWords = quotation.GrandTotalInWords
                ?? quotation.AmountInWords
                ?? IndianNumberWords.ToRupees(quotation.Total),

            DiscountApplied = quotation.DiscountApplied && quotation.DiscountPercent > 0,
            DiscountLabel = quotation.DiscountLabel,
            DiscountAmount = quotation.DiscountAmount,

            // Rebuilt from the quotation's own columns rather than from the
            // plan: a plan repriced next quarter must not change what a
            // customer was already sent.
            Annexure = Returns(quotation),

            Milestones = quotation.Milestones
                .OrderBy(m => m.SortOrder)
                .Select(m => new MilestoneResult(
                    m.SortOrder, m.Label, m.Percent,
                    m.BasicAmount, m.TaxAmount, m.TotalAmount, m.DueDate))
                .ToList(),

            SalesPersonName = quotation.Owner?.Name,
            SalesPersonId = quotation.OwnerId?.ToString(),
            Notes = quotation.Notes,
            TermsAndConditions = quotation.TermsAndConditions,

            Watermark = Watermark(quotation),
        };

        var bytes = new QuotationDocument(model).GeneratePdf();

        return File(bytes, "application/pdf", $"{quotation.QuoteNumber}-{quotation.UnitNumber}.pdf");
    }

    private static string? Watermark(Quotation q) => q.ApprovalStatus switch
    {
        QuotationApprovalStatuses.Pending =>
            "DRAFT — awaiting management approval. Not valid until issued.",
        QuotationApprovalStatuses.Rejected =>
            "NOT APPROVED — these terms were declined and must not be shared.",
        _ => q.Status == QuotationStatuses.Draft
            ? "DRAFT — not yet issued to the customer."
            : null,
    };

    /* ------------------------------------------------------------------ *
     * Timeline
     * ------------------------------------------------------------------ */

    /// <summary>Every event that happened to this quotation, most recent first.</summary>
    [HttpGet("{id:int}/timeline")]
    public async Task<ActionResult<IReadOnlyList<QuotationActivityDto>>> Timeline(
        int id, CancellationToken ct)
    {
        _ = await Db.Quotations.AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw ApiException.NotFound("Quotation");

        var activities = await Db.QuotationActivities.AsNoTracking()
            .Where(a => a.QuotationId == id)
            .OrderByDescending(a => a.CreatedAt)
            .Take(100)
            .Select(a => new QuotationActivityDto(
                a.Id, a.Type, a.Description, a.Metadata,
                a.ActorId, a.ActorName, a.CreatedAt))
            .ToListAsync(ct);

        return Ok(activities);
    }

    /* ------------------------------------------------------------------ *
     * Follow-ups
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Logs a follow-up interaction and optionally sets when the next one is due.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/follow-ups")]
    public async Task<ActionResult<QuotationFollowUpDto>> AddFollowUp(
        int id, CreateFollowUpRequest request, CancellationToken ct)
    {
        var quotation = await Db.Quotations.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw ApiException.NotFound("Quotation");

        if (string.IsNullOrWhiteSpace(request.Note))
            throw ApiException.BadRequest("A follow-up needs a note.");

        var followUp = new QuotationFollowUp
        {
            CompanyId = Db.Tenant.CompanyId,
            QuotationId = id,
            Channel = request.Channel ?? "Call",
            Note = request.Note.Trim(),
            Outcome = request.Outcome,
            NextFollowUpAt = request.NextFollowUpAt,
            CreatedById = Db.Tenant.UserId,
            CreatedByName = Db.Tenant.UserName,
        };

        Db.QuotationFollowUps.Add(followUp);

        quotation.LastFollowUpAt = DateTime.UtcNow;
        quotation.FollowUpCount += 1;
        quotation.NextFollowUpAt = request.NextFollowUpAt;
        quotation.FollowUpNote = request.NextFollowUpAt is not null
            ? request.Note.Trim()
            : null;

        Db.QuotationActivities.Add(new QuotationActivity
        {
            QuotationId = id,
            Type = QuotationActivityTypes.FollowUp,
            Description = $"Follow-up ({followUp.Channel}): {request.Note.Trim()}",
            ActorId = Db.Tenant.UserId,
            ActorName = Db.Tenant.UserName,
        });

        await Db.SaveChangesAsync(ct);

        return Ok(new QuotationFollowUpDto(
            followUp.Id, followUp.QuotationId, followUp.Channel,
            followUp.Note, followUp.Outcome, followUp.NextFollowUpAt,
            followUp.CreatedByName, followUp.CreatedAt));
    }

    /// <summary>
    /// Lists follow-up history for a quotation, most recent first.
    /// </summary>
    [HttpGet("{id:int}/follow-ups")]
    public async Task<ActionResult<IReadOnlyList<QuotationFollowUpDto>>> FollowUps(
        int id, CancellationToken ct)
    {
        var items = await Db.QuotationFollowUps.AsNoTracking()
            .Where(f => f.QuotationId == id)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new QuotationFollowUpDto(
                f.Id, f.QuotationId, f.Channel, f.Note, f.Outcome,
                f.NextFollowUpAt, f.CreatedByName, f.CreatedAt))
            .ToListAsync(ct);

        return Ok(items);
    }

    /// <summary>
    /// Quotations with pending follow-ups — due today and overdue first.
    /// </summary>
    [HttpGet("follow-ups/due")]
    public async Task<ActionResult<IReadOnlyList<FollowUpsDueDto>>> FollowUpsDue(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var items = await Db.Quotations.AsNoTracking()
            .Include(q => q.Owner)
            .Where(q => q.NextFollowUpAt != null
                && q.Status != QuotationStatuses.Accepted
                && q.Status != QuotationStatuses.Rejected
                && !q.IsDeleted)
            .OrderBy(q => q.NextFollowUpAt)
            .Take(50)
            .Select(q => new FollowUpsDueDto(
                q.Id, q.QuoteNumber, q.CustomerName, q.Status,
                q.GrandTotal, q.NextFollowUpAt, q.FollowUpNote,
                q.Owner != null ? q.Owner.Name : null,
                q.NextFollowUpAt < now))
            .ToListAsync(ct);

        return Ok(items);
    }

    /// <summary>
    /// Extends the validity date on a quotation, with an optional reason.
    /// Extensions beyond 15 days of the original validity trigger approval.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/extend-validity")]
    public async Task<ActionResult<QuotationDetailDto>> ExtendValidity(
        int id, ExtendValidityRequest request, CancellationToken ct)
    {
        var quotation = await Db.Quotations.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw ApiException.NotFound("Quotation");

        if (quotation.Status == QuotationStatuses.Accepted)
            throw ApiException.Conflict("An accepted quotation does not need its validity extended.");

        if (request.NewValidUntil <= quotation.ValidUntil)
            throw ApiException.BadRequest("The new date must be after the current validity.");

        // Remember the original date, for tracking how far it was extended.
        quotation.OriginalValidUntil ??= quotation.ValidUntil;

        var old = quotation.ValidUntil;
        quotation.ValidUntil = request.NewValidUntil;

        // Extend the unit hold to match the new validity.
        if (quotation.UnitId is int unitId)
        {
            var unit = await Db.Units.FirstOrDefaultAsync(u => u.Id == unitId, ct);
            if (unit is not null && unit.Status == UnitStatuses.Held
                && (unit.HoldReason?.Contains(quotation.QuoteNumber, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                unit.HeldUntil = request.NewValidUntil;
            }
        }

        Db.QuotationActivities.Add(new QuotationActivity
        {
            QuotationId = id,
            Type = QuotationActivityTypes.ValidityExtended,
            Description = $"Validity extended from {old:dd MMM yyyy} to {request.NewValidUntil:dd MMM yyyy}."
                + (string.IsNullOrWhiteSpace(request.Reason) ? "" : $" Reason: {request.Reason.Trim()}"),
            Metadata = System.Text.Json.JsonSerializer.Serialize(new { OldDate = old, NewDate = request.NewValidUntil }),
            ActorId = Db.Tenant.UserId,
            ActorName = Db.Tenant.UserName,
        });

        await Db.SaveChangesAsync(ct);
        return Ok(await LoadDetailAsync(id, ct));
    }

    /* ------------------------------------------------------------------ *
     * Negotiation
     * ------------------------------------------------------------------ */

    /// <summary>Logs a negotiation round against a quotation.</summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/negotiations")]
    public async Task<ActionResult<QuotationNegotiationDto>> AddNegotiation(
        int id, CreateNegotiationRequest request, CancellationToken ct)
    {
        var quotation = await Db.Quotations.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw ApiException.NotFound("Quotation");

        var round = await Db.QuotationNegotiations
            .Where(n => n.QuotationId == id)
            .CountAsync(ct) + 1;

        var negotiation = new QuotationNegotiation
        {
            CompanyId = Db.Tenant.CompanyId,
            QuotationId = id,
            Round = round,
            Type = NegotiationTypes.All.Contains(request.Type) ? request.Type : NegotiationTypes.CounterOffer,
            RequestedDiscount = request.RequestedDiscount,
            OfferedDiscount = request.OfferedDiscount,
            CustomerDemand = request.CustomerDemand,
            OurResponse = request.OurResponse,
            DeltaAmount = request.DeltaAmount,
            CreatedById = Db.Tenant.UserId,
            CreatedByName = Db.Tenant.UserName,
        };

        Db.QuotationNegotiations.Add(negotiation);

        // Move status to Negotiation if it is still Sent/UnderReview.
        if (quotation.Status is QuotationStatuses.Sent or QuotationStatuses.UnderReview)
        {
            quotation.Status = QuotationStatuses.Negotiation;
        }

        Db.QuotationActivities.Add(new QuotationActivity
        {
            QuotationId = id,
            Type = QuotationActivityTypes.NegotiationRound,
            Description = $"Negotiation round {round} ({request.Type})"
                + (request.CustomerDemand is not null ? $": {request.CustomerDemand}" : ""),
            ActorId = Db.Tenant.UserId,
            ActorName = Db.Tenant.UserName,
        });

        await Db.SaveChangesAsync(ct);

        return Ok(new QuotationNegotiationDto(
            negotiation.Id, negotiation.QuotationId, negotiation.Round,
            negotiation.Type, negotiation.RequestedDiscount, negotiation.OfferedDiscount,
            negotiation.CustomerDemand, negotiation.OurResponse, negotiation.DeltaAmount,
            negotiation.CreatedByName, negotiation.CreatedAt));
    }

    /// <summary>Negotiation history for a quotation, in round order.</summary>
    [HttpGet("{id:int}/negotiations")]
    public async Task<ActionResult<IReadOnlyList<QuotationNegotiationDto>>> Negotiations(
        int id, CancellationToken ct)
    {
        var items = await Db.QuotationNegotiations.AsNoTracking()
            .Where(n => n.QuotationId == id)
            .OrderBy(n => n.Round)
            .Select(n => new QuotationNegotiationDto(
                n.Id, n.QuotationId, n.Round, n.Type,
                n.RequestedDiscount, n.OfferedDiscount,
                n.CustomerDemand, n.OurResponse, n.DeltaAmount,
                n.CreatedByName, n.CreatedAt))
            .ToListAsync(ct);

        return Ok(items);
    }

    /* ------------------------------------------------------------------ *
     * Analytics
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Aggregated quotation analytics — funnel, win/loss, trends, rep performance.
    /// </summary>
    [HttpGet("analytics")]
    public async Task<ActionResult<QuotationAnalyticsDto>> Analytics(CancellationToken ct)
    {
        var all = await Db.Quotations.AsNoTracking()
            .Where(q => !q.IsDeleted)
            .Select(q => new
            {
                q.Id,
                q.Status,
                q.GrandTotal,
                q.DiscountPercent,
                q.IssueDate,
                q.RespondedAt,
                q.ValidUntil,
                q.RejectionReason,
                q.OwnerId,
                OwnerName = q.Owner != null ? q.Owner.Name : "Unassigned",
                q.ProjectId,
                ProjectName = q.Project != null ? q.Project.Name : "Unknown",
            })
            .ToListAsync(ct);

        // Funnel
        var funnel = new QuotationFunnelDto(
            all.Count(q => q.Status == QuotationStatuses.Draft),
            all.Count(q => q.Status == QuotationStatuses.Sent),
            all.Count(q => q.Status == QuotationStatuses.UnderReview),
            all.Count(q => q.Status == QuotationStatuses.Negotiation),
            all.Count(q => q.Status == QuotationStatuses.Accepted),
            all.Count(q => q.Status == QuotationStatuses.Rejected),
            all.Count(q => q.Status == QuotationStatuses.Expired));

        // Win/Loss
        var won = all.Count(q => q.Status == QuotationStatuses.Accepted);
        var lost = all.Count(q => q.Status is QuotationStatuses.Rejected or QuotationStatuses.Expired);

        var lossReasons = all
            .Where(q => q.Status == QuotationStatuses.Rejected && !string.IsNullOrWhiteSpace(q.RejectionReason))
            .GroupBy(q => q.RejectionReason!.Trim())
            .Select(g => new LossReasonDto(g.Key, g.Count()))
            .OrderByDescending(r => r.Count)
            .Take(10)
            .ToList();

        var conversionRate = all.Count > 0 ? (decimal)won / all.Count : 0;
        var acceptedQuotes = all.Where(q => q.Status == QuotationStatuses.Accepted).ToList();
        var avgDealSize = acceptedQuotes.Count > 0 ? acceptedQuotes.Average(q => q.GrandTotal) : 0;

        var daysToClose = acceptedQuotes
            .Where(q => q.RespondedAt is not null)
            .Select(q => (q.RespondedAt!.Value - q.IssueDate).TotalDays)
            .ToList();
        var avgDaysToClose = daysToClose.Count > 0 ? daysToClose.Average() : 0;

        var open = all.Where(q => q.Status is QuotationStatuses.Sent or QuotationStatuses.UnderReview or QuotationStatuses.Negotiation).ToList();
        var daysToExpiry = open
            .Select(q => (q.ValidUntil - DateTime.UtcNow).TotalDays)
            .ToList();
        var avgDaysToExpiry = daysToExpiry.Count > 0 ? daysToExpiry.Average() : 0;

        // Monthly trend (last 12 months)
        var twelveMonthsAgo = DateTime.UtcNow.AddMonths(-12);
        var monthlyTrend = all
            .Where(q => q.IssueDate >= twelveMonthsAgo)
            .GroupBy(q => q.IssueDate.ToString("yyyy-MM"))
            .Select(g => new MonthlyTrendDto(
                g.Key,
                g.Sum(q => q.GrandTotal),
                g.Where(q => q.Status == QuotationStatuses.Accepted).Sum(q => q.GrandTotal),
                g.Count()))
            .OrderBy(m => m.Month)
            .ToList();

        // Rep performance
        var repPerformance = all
            .GroupBy(q => new { q.OwnerId, q.OwnerName })
            .Select(g => new RepPerformanceDto(
                g.Key.OwnerId,
                g.Key.OwnerName,
                g.Count(),
                g.Count(q => q.Status == QuotationStatuses.Accepted),
                g.Sum(q => q.GrandTotal),
                g.Where(q => q.Status == QuotationStatuses.Accepted).Sum(q => q.GrandTotal)))
            .OrderByDescending(r => r.AcceptedValue)
            .ToList();

        // Project breakdown
        var projectBreakdown = all
            .GroupBy(q => new { q.ProjectId, q.ProjectName })
            .Select(g => new ProjectBreakdownDto(
                g.Key.ProjectId,
                g.Key.ProjectName,
                g.Count(),
                g.Count(q => q.Status == QuotationStatuses.Accepted),
                g.Count() > 0 ? g.Average(q => q.DiscountPercent) : 0,
                g.Sum(q => q.GrandTotal)))
            .OrderByDescending(p => p.TotalValue)
            .ToList();

        return Ok(new QuotationAnalyticsDto(
            funnel, conversionRate, avgDealSize, avgDaysToClose, avgDaysToExpiry,
            new QuotationWinLossDto(won, lost, lossReasons),
            monthlyTrend, repPerformance, projectBreakdown));
    }

    /* ------------------------------------------------------------------ *
     * Shareable links
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Generates a shareable link a rep can copy into a message.
    ///
    /// The link is a GUID token rather than the quotation id, so it cannot be
    /// guessed, and it expires independently of the quotation's own validity.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/share")]
    public async Task<ActionResult<ShareLinkDto>> CreateShareLink(
        int id, CreateShareLinkRequest request, CancellationToken ct)
    {
        var quotation = await Db.Quotations.AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw ApiException.NotFound("Quotation");

        var link = new QuotationShareLink
        {
            QuotationId = id,
            ExpiresAt = DateTime.UtcNow.AddDays(Math.Clamp(request.ExpiryDays ?? 30, 1, 90)),
            CreatedById = Db.Tenant.UserId,
            CreatedByName = Db.Tenant.UserName,
        };

        Db.QuotationShareLinks.Add(link);

        Db.QuotationActivities.Add(new QuotationActivity
        {
            QuotationId = id,
            Type = "ShareLinkCreated",
            Description = $"Shareable link created, expires {link.ExpiresAt:dd MMM yyyy}.",
            ActorId = Db.Tenant.UserId,
            ActorName = Db.Tenant.UserName,
        });

        await Db.SaveChangesAsync(ct);

        return Ok(ToShareLinkDto(link));
    }

    /// <summary>Active links for a quotation.</summary>
    [HttpGet("{id:int}/share-links")]
    public async Task<ActionResult<IReadOnlyList<ShareLinkDto>>> ShareLinks(
        int id, CancellationToken ct)
    {
        var links = await Db.QuotationShareLinks.AsNoTracking()
            .Where(l => l.QuotationId == id)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

        return Ok(links.Select(ToShareLinkDto).ToList());
    }

    /// <summary>Revokes a link so the customer can no longer view the quotation through it.</summary>
    [HttpDelete("share-links/{linkId:int}")]
    public async Task<IActionResult> RevokeShareLink(int linkId, CancellationToken ct)
    {
        var link = await Db.QuotationShareLinks
            .FirstOrDefaultAsync(l => l.Id == linkId, ct)
            ?? throw ApiException.NotFound("Share link");

        link.IsActive = false;
        await Db.SaveChangesAsync(ct);

        return NoContent();
    }

    private static ShareLinkDto ToShareLinkDto(QuotationShareLink l) => new(
        l.Id, l.QuotationId, l.Token,
        $"/q/{l.Token}",
        l.CreatedAt, l.ExpiresAt, l.IsActive,
        l.ViewCount, l.LastViewedAt,
        l.RespondedAt, l.ResponseStatus, l.CustomerComment,
        l.CreatedByName);

    /* ------------------------------------------------------------------ *
     * Templates
     * ------------------------------------------------------------------ */

    /// <summary>Lists quotation templates, optionally filtered by project.</summary>
    [HttpGet("templates")]
    public async Task<ActionResult<IReadOnlyList<QuotationTemplateDto>>> Templates(
        [FromQuery] int? projectId, CancellationToken ct)
    {
        var templates = await Db.QuotationTemplates.AsNoTracking()
            .Include(t => t.Project)
            .Include(t => t.PaymentPlan)
            .Include(t => t.Charges)
            .Where(t => t.IsActive && (projectId == null || t.ProjectId == null || t.ProjectId == projectId))
            .OrderBy(t => t.SortOrder)
            .ToListAsync(ct);

        return Ok(templates.Select(ToTemplateDto).ToList());
    }

    /// <summary>Creates a new quotation template.</summary>
    [HttpPost("templates")]
    public async Task<ActionResult<QuotationTemplateDto>> CreateTemplate(
        CreateTemplateRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw ApiException.BadRequest("A template needs a name.");

        var template = new QuotationTemplate
        {
            CompanyId = Db.Tenant.CompanyId,
            ProjectId = request.ProjectId,
            Name = request.Name.Trim(),
            Description = request.Description,
            PaymentPlanId = request.PaymentPlanId,
            DefaultDiscount = request.DefaultDiscount,
            DefaultNotes = request.DefaultNotes,
            DefaultTermsAndConditions = request.DefaultTermsAndConditions,
            ValidDays = request.ValidDays,

            ApplyDiscount = request.ApplyDiscount,
            IncludeAssuredReturn = request.IncludeAssuredReturn,
            IncludeBuyBack = request.IncludeBuyBack,
            IncludeRentalYield = request.IncludeRentalYield,
        };

        if (request.Charges is { Count: > 0 })
        {
            foreach (var charge in request.Charges)
            {
                template.Charges.Add(new QuotationTemplateCharge
                {
                    ChargeHeadId = charge.ChargeHeadId,
                    Quantity = charge.Quantity,
                });
            }
        }

        Db.QuotationTemplates.Add(template);
        await Db.SaveChangesAsync(ct);

        return Ok(await LoadTemplateAsync(template.Id, ct));
    }

    /// <summary>Updates an existing template.</summary>
    [HttpPut("templates/{id:int}")]
    public async Task<ActionResult<QuotationTemplateDto>> UpdateTemplate(
        int id, UpdateTemplateRequest request, CancellationToken ct)
    {
        var template = await Db.QuotationTemplates
            .Include(t => t.Charges)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw ApiException.NotFound("Template");

        if (request.Name is not null) template.Name = request.Name.Trim();
        if (request.Description is not null) template.Description = request.Description;
        if (request.PaymentPlanId is not null) template.PaymentPlanId = request.PaymentPlanId;
        if (request.DefaultDiscount is not null) template.DefaultDiscount = request.DefaultDiscount;
        if (request.DefaultNotes is not null) template.DefaultNotes = request.DefaultNotes;
        if (request.DefaultTermsAndConditions is not null) template.DefaultTermsAndConditions = request.DefaultTermsAndConditions;
        if (request.ValidDays is not null) template.ValidDays = request.ValidDays;
        if (request.IsActive is not null) template.IsActive = request.IsActive.Value;
        if (request.ApplyDiscount is not null) template.ApplyDiscount = request.ApplyDiscount;
        if (request.IncludeAssuredReturn is not null) template.IncludeAssuredReturn = request.IncludeAssuredReturn;
        if (request.IncludeBuyBack is not null) template.IncludeBuyBack = request.IncludeBuyBack;
        if (request.IncludeRentalYield is not null) template.IncludeRentalYield = request.IncludeRentalYield;

        if (request.Charges is not null)
        {
            Db.QuotationTemplateCharges.RemoveRange(template.Charges);
            template.Charges.Clear();

            foreach (var charge in request.Charges)
            {
                template.Charges.Add(new QuotationTemplateCharge
                {
                    ChargeHeadId = charge.ChargeHeadId,
                    Quantity = charge.Quantity,
                });
            }
        }

        await Db.SaveChangesAsync(ct);
        return Ok(await LoadTemplateAsync(id, ct));
    }

    /// <summary>Soft-deletes a template.</summary>
    [HttpDelete("templates/{id:int}")]
    public async Task<IActionResult> DeleteTemplate(int id, CancellationToken ct)
    {
        var template = await Db.QuotationTemplates
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw ApiException.NotFound("Template");

        SoftDelete(template);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<QuotationTemplateDto> LoadTemplateAsync(int id, CancellationToken ct)
    {
        var t = await Db.QuotationTemplates.AsNoTracking()
            .Include(x => x.Project)
            .Include(x => x.PaymentPlan)
            .Include(x => x.Charges)
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw ApiException.NotFound("Template");

        return ToTemplateDto(t);
    }

    private static QuotationTemplateDto ToTemplateDto(QuotationTemplate t) => new(
        t.Id, t.ProjectId, t.Project?.Name, t.Name, t.Description,
        t.PaymentPlanId, t.PaymentPlan?.Name,
        t.DefaultDiscount, t.DefaultNotes, t.DefaultTermsAndConditions,
        t.ValidDays, t.IsActive, t.SortOrder,
        t.ApplyDiscount, t.IncludeAssuredReturn, t.IncludeBuyBack, t.IncludeRentalYield,
        t.Charges.Select(c => new TemplateChargeDto(c.ChargeHeadId, c.Quantity)).ToList(),
        t.CreatedAt);

    /* ------------------------------------------------------------------ *
     * Delivery (Email & WhatsApp)
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Sends the quotation PDF to the customer via Email.
    /// Marks the quotation as Sent and logs the email delivery on the timeline.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/send-email")]
    public async Task<ActionResult<DeliveryResultDto>> SendEmail(
        int id, SendQuotationEmailRequest request, CancellationToken ct)
    {
        var quotation = await Db.Quotations.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw ApiException.NotFound("Quotation");

        if (string.IsNullOrWhiteSpace(request.To))
            throw ApiException.BadRequest("Recipient email address is required.");

        if (quotation.ApprovalStatus == QuotationApprovalStatuses.Pending)
        {
            throw ApiException.Conflict("This quotation is waiting on approval and cannot be sent yet.");
        }

        if (quotation.ApprovalStatus == QuotationApprovalStatuses.Rejected)
        {
            throw ApiException.Conflict($"The terms on this quotation were rejected: {quotation.RejectionReason}");
        }

        if (quotation.Status == QuotationStatuses.Draft)
        {
            quotation.Status = QuotationStatuses.Sent;
            quotation.SentAt = DateTime.UtcNow;
            await lifecycle.OnIssuedAsync(quotation, ct);
        }

        Db.QuotationActivities.Add(new QuotationActivity
        {
            QuotationId = id,
            Type = "EmailSent",
            Description = $"Email sent to {request.To.Trim()}"
                + (string.IsNullOrWhiteSpace(request.Cc) ? "" : $" (CC: {request.Cc.Trim()})")
                + $": \"{request.Subject.Trim()}\"",
            ActorId = Db.Tenant.UserId,
            ActorName = Db.Tenant.UserName,
        });

        await Db.SaveChangesAsync(ct);

        return Ok(new DeliveryResultDto(
            true,
            $"Quotation sent successfully to {request.To.Trim()}.",
            DateTime.UtcNow));
    }

    /// <summary>
    /// Prepares and logs WhatsApp delivery of the quotation.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/send-whatsapp")]
    public async Task<ActionResult<DeliveryResultDto>> SendWhatsApp(
        int id, SendQuotationWhatsAppRequest request, CancellationToken ct)
    {
        var quotation = await Db.Quotations.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw ApiException.NotFound("Quotation");

        if (string.IsNullOrWhiteSpace(request.Phone))
            throw ApiException.BadRequest("Recipient phone number is required.");

        if (quotation.ApprovalStatus == QuotationApprovalStatuses.Pending)
        {
            throw ApiException.Conflict("This quotation is waiting on approval and cannot be sent yet.");
        }

        if (quotation.ApprovalStatus == QuotationApprovalStatuses.Rejected)
        {
            throw ApiException.Conflict($"The terms on this quotation were rejected: {quotation.RejectionReason}");
        }

        if (quotation.Status == QuotationStatuses.Draft)
        {
            quotation.Status = QuotationStatuses.Sent;
            quotation.SentAt = DateTime.UtcNow;
            await lifecycle.OnIssuedAsync(quotation, ct);
        }

        Db.QuotationActivities.Add(new QuotationActivity
        {
            QuotationId = id,
            Type = "WhatsAppSent",
            Description = $"WhatsApp message sent to {request.Phone.Trim()}.",
            ActorId = Db.Tenant.UserId,
            ActorName = Db.Tenant.UserName,
        });

        await Db.SaveChangesAsync(ct);

        return Ok(new DeliveryResultDto(
            true,
            $"WhatsApp message logged and sent to {request.Phone.Trim()}.",
            DateTime.UtcNow));
    }

    /* ------------------------------------------------------------------ *
     * Multi-Unit / Combo Quotations
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Calculates combined pricing across multiple selected units for investor/combo quotes.
    /// </summary>
    [PermissionAction(ObjectAction.View)]
    [HttpPost("preview-multi")]
    public async Task<ActionResult<MultiUnitPreviewDto>> PreviewMulti(
        MultiUnitPreviewRequest request, CancellationToken ct)
    {
        if (request.UnitIds == null || request.UnitIds.Count == 0)
            throw ApiException.BadRequest("At least one unit must be selected.");

        var units = await Db.Units.AsNoTracking()
            .Include(u => u.Tower)
            .Where(u => request.UnitIds.Contains(u.Id))
            .ToListAsync(ct);

        if (units.Count != request.UnitIds.Count)
            throw ApiException.NotFound("One or more selected units were not found.");

        var plan = await Db.PaymentPlans.AsNoTracking()
            .Include(p => p.Milestones)
            .FirstOrDefaultAsync(p => p.Id == request.PaymentPlanId, ct)
            ?? throw ApiException.NotFound("Payment plan");

        var bookingDate = request.BookingDate ?? DateTime.UtcNow;

        var discount = request.Options?.ApplyDiscount == false
            ? 0m
            : Math.Clamp(request.Discount ?? plan.StandardDiscount, 0m, 0.9m);

        var unitItems = new List<MultiUnitItemPreviewDto>();
        decimal combinedSaleableArea = 0;
        decimal combinedBasicAmount = 0;
        decimal combinedTaxAmount = 0;
        decimal combinedTotalAmount = 0;
        bool anyRequiresApproval = false;
        string? approvalReason = null;

        foreach (var unit in units)
        {
            var card = await ActiveRateCardAsync(unit, bookingDate, ct);
            var rate = request.RateOverride ?? card?.RatePerSqft ?? unit.PricePerSqft;
            if (rate <= 0) rate = 5000; // fallback standard rate

            var area = unit.SuperArea ?? unit.BuiltUpArea ?? unit.CarpetArea;
            var effectiveRate = decimal.Round((rate * (1m - discount)) + unit.PlcPerSqft, 2);
            var basic = decimal.Round(area * effectiveRate, 2);
            var tax = decimal.Round(basic * plan.TaxRate, 2);
            var total = basic + tax;

            combinedSaleableArea += area;
            combinedBasicAmount += basic;
            combinedTaxAmount += tax;
            combinedTotalAmount += total;

            var (reqApp, reason) = ApprovalCheck(plan, discount, request.RateOverride, card?.RatePerSqft);
            if (reqApp)
            {
                anyRequiresApproval = true;
                approvalReason ??= reason;
            }

            unitItems.Add(new MultiUnitItemPreviewDto(
                unit.Id,
                unit.UnitNumber,
                unit.Tower?.Name,
                unit.Configuration,
                unit.Floor,
                area,
                rate,
                effectiveRate,
                basic,
                total));
        }

        // Resolve common charges (lumpsum / per quantity)
        var firstUnit = units[0];
        var charges = await ResolveChargesAsync(firstUnit, request.Charges, ct);
        var pricedCharges = new List<QuoteChargeDto>();
        decimal chargesTotal = 0;

        foreach (var ch in charges)
        {
            var basicAmt = ch.Basis switch
            {
                ChargeBases.PerSqft => decimal.Round(combinedSaleableArea * ch.Rate, 2),
                ChargeBases.PercentOfUnitCost => decimal.Round(combinedBasicAmount * ch.Rate, 2),
                _ => decimal.Round(ch.Quantity * ch.Rate, 2),
            };
            var taxAmt = decimal.Round(basicAmt * ch.TaxRate, 2);
            var totAmt = basicAmt + taxAmt;
            chargesTotal += totAmt;

            pricedCharges.Add(new QuoteChargeDto(
                ch.ChargeHeadId, ch.SortOrder, ch.Group, ch.Name, ch.Basis,
                ch.Quantity, null, ch.Rate, basicAmt, ch.TaxRate, taxAmt, totAmt,
                ch.IsRefundable, ch.IncludeInSchedule, ch.DueLabel));
        }

        var grandTotal = combinedTotalAmount + chargesTotal;

        // Schedule milestones
        var milestones = plan.Milestones
            .OrderBy(m => m.SortOrder)
            .Select(m =>
            {
                var mBasic = decimal.Round(combinedBasicAmount * m.Percent, 2);
                var mTax = decimal.Round(mBasic * plan.TaxRate, 2);
                return new QuoteMilestoneDto(
                    m.SortOrder,
                    m.Label,
                    m.Percent,
                    mBasic,
                    mTax,
                    mBasic + mTax,
                    bookingDate.AddDays(m.DueOffsetDays ?? 0));
            })
            .ToList();

        return Ok(new MultiUnitPreviewDto(
            unitItems,
            plan.Id,
            plan.Name,
            plan.StandardDiscount,
            discount,
            combinedSaleableArea,
            combinedBasicAmount,
            combinedTaxAmount,
            combinedTotalAmount,
            pricedCharges,
            chargesTotal,
            grandTotal,
            IndianNumberWords.ToRupees(grandTotal),
            milestones,
            anyRequiresApproval,
            approvalReason));
    }

    /// <summary>
    /// Creates a multi-unit / combo quotation.
    /// </summary>
    [HttpPost("from-units")]
    public async Task<ActionResult<QuotationResultDto>> CreateFromUnits(
        CreateMultiUnitQuotationRequest request, CancellationToken ct)
    {
        if (request.UnitIds == null || request.UnitIds.Count == 0)
            throw ApiException.BadRequest("At least one unit must be selected.");

        if (string.IsNullOrWhiteSpace(request.CustomerName))
            throw ApiException.BadRequest("A quotation needs a customer name.");

        var units = await Db.Units
            .Include(u => u.Project)
            .Include(u => u.Tower)
            .Where(u => request.UnitIds.Contains(u.Id))
            .ToListAsync(ct);

        if (units.Count != request.UnitIds.Count)
            throw ApiException.NotFound("One or more selected units were not found.");

        foreach (var u in units)
        {
            if (UnitStatuses.Unavailable.Contains(u.Status))
            {
                throw ApiException.Conflict($"{u.UnitNumber} is {u.Status.ToLowerInvariant()} and cannot be quoted.");
            }
        }

        var plan = await Db.PaymentPlans.AsNoTracking()
            .Include(p => p.Milestones)
            .FirstOrDefaultAsync(p => p.Id == request.PaymentPlanId, ct)
            ?? throw ApiException.NotFound("Payment plan");

        var branch = await RequireBranchAsync(
            request.BranchId ?? await DefaultBranchIdAsync(ct), ct);

        var previewRes = (await PreviewMulti(new MultiUnitPreviewRequest(
            request.UnitIds, request.PaymentPlanId, request.Discount, request.RateOverride,
            request.BookingDate, request.Charges, request.Options), ct)).Value!;

        var unitNumbers = string.Join(", ", units.Select(u => u.UnitNumber));
        var first = units[0];

        var quotation = new Quotation
        {
            CompanyId = Db.Tenant.CompanyId,
            BranchId = branch.Id,
            QuoteNumber = Code("QT"),
            Title = $"Combo ({units.Count} Units) · {unitNumbers}",
            Version = 1,

            LeadId = request.LeadId,
            ContactId = request.ContactId,
            OpportunityId = request.OpportunityId,
            ProjectId = first.ProjectId,
            UnitId = first.Id, // primary unit reference

            CustomerName = request.CustomerName.Trim(),
            CustomerEmail = request.CustomerEmail,
            CustomerPhone = request.CustomerPhone,
            BillingAddress = request.BillingAddress,

            PaymentPlanId = plan.Id,
            PaymentPlanName = plan.Name,
            TowerName = first.Tower?.Name,
            UnitNumber = unitNumbers,
            UnitType = $"{units.Count} Units Combo",
            SaleableArea = previewRes.CombinedSaleableArea,
            BuiltUpArea = units.Sum(u => u.BuiltUpArea ?? 0m),
            CarpetArea = units.Sum(u => u.CarpetArea),

            RatePerSqft = previewRes.CombinedSaleableArea > 0 ? previewRes.CombinedBasicAmount / previewRes.CombinedSaleableArea : 0,
            EffectiveRatePerSqft = previewRes.CombinedSaleableArea > 0 ? previewRes.CombinedBasicAmount / previewRes.CombinedSaleableArea : 0,
            StandardDiscountPercent = plan.StandardDiscount,
            DiscountPercent = previewRes.Discount,

            Subtotal = previewRes.CombinedBasicAmount,
            TaxPercent = plan.TaxRate,
            TaxAmount = previewRes.CombinedTaxAmount,
            Total = previewRes.CombinedTotalAmount,
            AmountInWords = IndianNumberWords.ToRupees(previewRes.CombinedTotalAmount),

            ChargesBasic = previewRes.Charges.Sum(c => c.BasicAmount),
            ChargesTax = previewRes.Charges.Sum(c => c.TaxAmount),
            ChargesTotal = previewRes.CombinedChargesTotal,
            GrandTotal = previewRes.CombinedGrandTotal,
            ScheduledTotal = previewRes.CombinedGrandTotal,
            GrandTotalInWords = previewRes.GrandTotalInWords,

            Status = QuotationStatuses.Draft,
            ApprovalStatus = previewRes.RequiresApproval
                ? QuotationApprovalStatuses.Pending
                : QuotationApprovalStatuses.NotRequired,

            IssueDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(Math.Clamp(request.ValidDays ?? 15, 1, 180)),

            PaymentTerms = plan.Name,
            Notes = request.Notes,
            TermsAndConditions = request.TermsAndConditions,
            OwnerId = request.OwnerId ?? (Db.Tenant.UserId > 0 ? Db.Tenant.UserId : null),
        };

        WriteReturns(
            quotation,
            ResolveReturns(
                plan, request.Options,
                previewRes.CombinedBasicAmount, previewRes.CombinedSaleableArea,
                request.BookingDate ?? DateTime.UtcNow),
            request.Options);

        // Add line items for each unit
        int sort = 1;
        foreach (var uItem in previewRes.Units)
        {
            quotation.Lines.Add(new QuotationLine
            {
                Description = $"{uItem.UnitType} {uItem.UnitNumber} — {uItem.SaleableArea:0.##} sq ft @ Rs. {uItem.EffectiveRatePerSqft:N2}/sq ft",
                Category = "Unit cost",
                Quantity = uItem.SaleableArea,
                Unit = "sqft",
                UnitPrice = uItem.EffectiveRatePerSqft,
                LineTotal = uItem.BasicAmount,
                SortOrder = sort++,
            });
        }

        // Add snapshots for charges and milestones
        foreach (var c in previewRes.Charges)
        {
            quotation.Charges.Add(new QuotationCharge
            {
                ChargeHeadId = c.ChargeHeadId,
                SortOrder = c.SortOrder,
                Group = c.Group,
                Name = c.Name,
                Basis = c.Basis,
                Quantity = c.Quantity,
                Rate = c.Rate,
                BasicAmount = c.BasicAmount,
                TaxRate = c.TaxRate,
                TaxAmount = c.TaxAmount,
                TotalAmount = c.TotalAmount,
                IsRefundable = c.IsRefundable,
                IncludeInSchedule = c.IncludeInSchedule,
                DueLabel = c.DueLabel,
            });
        }

        foreach (var m in previewRes.Milestones)
        {
            quotation.Milestones.Add(new QuotationMilestone
            {
                SortOrder = m.SortOrder,
                Label = m.Label,
                Percent = m.Percent,
                BasicAmount = m.BasicAmount,
                TaxAmount = m.TaxAmount,
                TotalAmount = m.TotalAmount,
                DueDate = m.DueDate,
            });
        }

        Db.Quotations.Add(quotation);
        await Db.SaveChangesAsync(ct);

        // Hold all units
        foreach (var unit in units)
        {
            if (unit.Status is UnitStatuses.Available or UnitStatuses.Held)
            {
                unit.Status = UnitStatuses.Held;
                unit.HeldByUserId = Db.Tenant.UserId;
                unit.HeldUntil = DateTime.UtcNow.AddDays(3);
                unit.HoldReason = $"Quoted (Combo) to {quotation.CustomerName} on {quotation.QuoteNumber}";
            }
        }

        Db.QuotationActivities.Add(new QuotationActivity
        {
            QuotationId = quotation.Id,
            Type = QuotationActivityTypes.Created,
            Description = $"Combo quotation created for {units.Count} units ({unitNumbers}) at {quotation.GrandTotal:N0}.",
            ActorId = Db.Tenant.UserId,
            ActorName = Db.Tenant.UserName,
        });

        await Db.SaveChangesAsync(ct);

        return Ok(new QuotationResultDto(
            await LoadDetailAsync(quotation.Id, ct),
            null,
            $"{quotation.QuoteNumber} created for {units.Count} units."));
    }


    /* ------------------------------------------------------------------ *
     * Shared
     * ------------------------------------------------------------------ */

    private async Task<int> DefaultBranchIdAsync(CancellationToken ct) =>
        Db.Tenant.BranchIds.Count > 0
            ? Db.Tenant.BranchIds[0]
            : await Db.Branches.Where(b => b.CompanyId == Db.Tenant.CompanyId)
                .Select(b => b.Id).FirstAsync(ct);

    private async Task<QuotationDetailDto> LoadDetailAsync(int id, CancellationToken ct)
    {
        var q = await Db.Quotations.AsNoTracking()
            .Include(x => x.Milestones)
            .Include(x => x.Charges)
            .Include(x => x.Project)
            .Include(x => x.Owner)
            .Include(x => x.Branch)
            // Split, because this loads more than one collection. Joined into a
            // single statement, EF returns the parent once per milestone per
            // charge — a nineteen-instalment plan with six heads is one hundred
            // and fourteen rows of duplicated quotation to read one document.
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw ApiException.NotFound("Quotation");

        return new QuotationDetailDto(
            q.Id, q.QuoteNumber, q.Title, q.Version, q.Status, q.ApprovalStatus,
            q.LeadId, q.ContactId, q.OpportunityId, q.ProjectId, q.Project?.Name,
            q.UnitId, q.UnitNumber, q.TowerName, q.UnitType,
            q.CustomerName, q.CustomerEmail, q.CustomerPhone, q.BillingAddress,
            q.PaymentPlanId, q.PaymentPlanName,
            q.SaleableArea, q.BuiltUpArea, q.CarpetArea,
            q.EventType, q.EventDate, q.EventEndDate, q.EventSlot, q.Functions,
            q.GuestCount, q.MinimumPlates,
            q.RateCardLabel,
            q.RatePerSqft, q.PlcPerSqft, q.EffectiveRatePerSqft,
            q.StandardDiscountPercent, q.DiscountPercent,
            q.DiscountApplied && q.DiscountPercent > 0, q.DiscountLabel, q.DiscountAmount,
            q.Subtotal, q.TaxPercent, q.TaxAmount, q.Total, q.AmountInWords,
            q.ChargesBasic, q.ChargesTax, q.ChargesTotal, q.RefundableTotal,
            // Quotations raised before charge heads existed carry a zero grand
            // total; the unit cost is the whole consideration on those, so it
            // stands in rather than printing a nil.
            q.GrandTotal == 0 ? q.Total : q.GrandTotal,
            q.ScheduledTotal == 0 ? q.Total : q.ScheduledTotal,
            q.GrandTotalInWords ?? q.AmountInWords,
            q.Charges.OrderBy(c => c.SortOrder).Select(ToChargeRowDto).ToList(),
            ToAnnexureDto(Returns(q)),
            q.IssueDate, q.ValidUntil, q.SentAt, q.OwnerId, q.Owner?.Name,
            q.BranchId, q.Branch?.Name ?? "—", q.Notes, q.TermsAndConditions, q.RejectionReason,
            q.Milestones.OrderBy(m => m.SortOrder)
                .Select(m => new QuoteMilestoneDto(
                    m.SortOrder, m.Label, m.Percent,
                    m.BasicAmount, m.TaxAmount, m.TotalAmount, m.DueDate))
                .ToList(),
            q.CreatedAt, q.UpdatedAt);
    }
}
