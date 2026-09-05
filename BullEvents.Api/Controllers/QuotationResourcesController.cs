using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// What it takes to deliver a proposal, and what that leaves.
///
/// This is the seam between the commercial side of the CRM and the four
/// operational inventories. A quotation has always known what the client pays;
/// what it could not say was what delivering it costs, because the props, the
/// crew, the suppliers and the trucks each lived in their own module.
///
/// Three things happen here:
///
///   1. <b>Planning.</b> Props, kits, crew and vendor rates are priced onto the
///      proposal with a cost beside the sell price, so the margin is arithmetic.
///   2. <b>Holding.</b> A live proposal can softly hold its stock, expiring with
///      its own validity date — so a deal that goes quiet hands the crates back
///      without anybody remembering to.
///   3. <b>Conversion.</b> An accepted proposal becomes a gate pass, a set of
///      crew assignments and a purchase order per supplier, in one action and
///      without retyping.
///
/// Shares the <c>api/quotations</c> prefix with the builder, the way
/// <see cref="VenuePackagesController"/> shares <c>api/inventory</c>.
/// </summary>
[ApiController]
[Route("api/quotations")]
[Authorize]
[SecuredBy(SecuredObjects.Quotation)]
public class QuotationResourcesController(AppDbContext db) : CrmControllerBase(db)
{
    private IQueryable<QuotationResource> Base() => Db.QuotationResources
        .Include(r => r.PropItem).ThenInclude(i => i!.Photos)
        .Include(r => r.PropKit)
        .Include(r => r.CrewMember)
        .Include(r => r.Vendor)
        .Include(r => r.VendorRate);

    /* ------------------------------------------------------------------ *
     * Dates
     * ------------------------------------------------------------------ */

    /// <summary>
    /// The window a proposal's resources occupy, when a line does not name its
    /// own.
    ///
    /// Props leave the day before the function and come back the day after,
    /// because that is what actually happens — quoting a mandap for the wedding
    /// day alone would let a second event book the same pillars for the setup
    /// morning. A proposal with no event date yet falls back to its validity,
    /// which at least keeps the arithmetic honest until a date is fixed.
    /// </summary>
    private static (DateOnly From, DateOnly To) WindowFor(Quotation q)
    {
        if (q.EventDate is DateTime start)
        {
            var end = q.EventEndDate ?? start;
            return (
                DateOnly.FromDateTime(start).AddDays(-1),
                DateOnly.FromDateTime(end).AddDays(1));
        }

        var fallback = DateOnly.FromDateTime(q.ValidUntil);
        return (fallback, fallback.AddDays(1));
    }

    private static int DaysIn(DateOnly from, DateOnly to) =>
        Math.Max(1, to.DayNumber - from.DayNumber + 1);

    /* ------------------------------------------------------------------ *
     * Availability
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Stock held by everyone else over a window.
    ///
    /// This proposal's own holds are excluded by reservation id rather than by
    /// quotation, because a line being repriced must be measured against what
    /// it can grow to, not against itself.
    /// </summary>
    private async Task<Dictionary<int, int>> HeldElsewhereAsync(
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<int> itemIds,
        IReadOnlyCollection<int> ignoreReservationIds,
        CancellationToken ct)
    {
        if (itemIds.Count == 0) return [];

        var now = DateTime.UtcNow;

        var query = Db.PropReservations.AsNoTracking()
            .Where(r => r.Status == PropIssueStatuses.Reserved)
            .Where(r => r.FromDate <= to && r.ToDate >= from)
            .Where(r => r.ExpiresAt == null || r.ExpiresAt > now)
            .Where(r => itemIds.Contains(r.PropItemId));

        if (ignoreReservationIds.Count > 0)
        {
            query = query.Where(r => !ignoreReservationIds.Contains(r.Id));
        }

        return await query
            .GroupBy(r => r.PropItemId)
            .Select(g => new { ItemId = g.Key, Held = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.ItemId, x => x.Held, ct);
    }

    /* ------------------------------------------------------------------ *
     * Reading the plan
     * ------------------------------------------------------------------ */

    [HttpGet("{id:int}/resources")]
    public async Task<ActionResult<QuotationMarginDto>> Plan(int id, CancellationToken ct)
    {
        return Ok(await BuildMarginAsync(id, ct));
    }

    /// <summary>The margin sheet on its own — the number a manager opens.</summary>
    [HttpGet("{id:int}/margin")]
    public async Task<ActionResult<QuotationMarginDto>> Margin(int id, CancellationToken ct)
    {
        return Ok(await BuildMarginAsync(id, ct));
    }

    /// <summary>
    /// What can be put on this proposal, for its own dates.
    ///
    /// Deliberately one call: the planner choosing décor is weighing a kit
    /// against a few loose props against a florist, and three round trips would
    /// have made that three separate screens.
    /// </summary>
    [HttpGet("{id:int}/resource-options")]
    public async Task<ActionResult<QuotationResourceOptionsDto>> Options(
        int id,
        [FromQuery] string? search,
        [FromQuery] int? categoryId,
        CancellationToken ct = default)
    {
        var quotation = await Db.Quotations.AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw ApiException.NotFound("Quotation");

        var (from, to) = WindowFor(quotation);

        /* ---------------- kits ---------------- */

        var kits = await Db.PropKits.AsNoTracking()
            .Include(k => k.Lines).ThenInclude(l => l.Item).ThenInclude(i => i!.Category)
            .Include(k => k.Lines).ThenInclude(l => l.Item).ThenInclude(i => i!.Photos)
            .Where(k => k.IsActive)
            .OrderBy(k => k.Name)
            .Take(50)
            .ToListAsync(ct);

        /* ---------------- props ---------------- */

        var propQuery = Db.PropItems.AsNoTracking()
            .Include(i => i.Category)
            .Include(i => i.Photos)
            .Where(i => i.Status == PropItemStatuses.Active && i.GoodQuantity > 0);

        if (categoryId is int cid) propQuery = propQuery.Where(i => i.CategoryId == cid);

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Folded and re-collated to "C": every string column carries the
            // app's non-deterministic ICU collation and Postgres will not run
            // LIKE against one. Same fix as QueryEngine.CaseInsensitiveText.
            var needle = search.Trim().ToLowerInvariant();
            propQuery = propQuery.Where(i =>
                EF.Functions.Collate(i.Name.ToLower(), "C").Contains(needle)
                || EF.Functions.Collate(i.Code.ToLower(), "C").Contains(needle));
        }

        var props = await propQuery.OrderBy(i => i.Name).Take(120).ToListAsync(ct);

        var itemIds = props.Select(p => p.Id)
            .Concat(kits.SelectMany(k => k.Lines).Select(l => l.PropItemId))
            .Distinct()
            .ToList();

        // This proposal's own holds do not count against it — a line already
        // holding twelve vases should read as twelve available, not zero.
        var ownReservations = await Db.QuotationResources.AsNoTracking()
            .Where(r => r.QuotationId == id && r.PropReservationId != null)
            .Select(r => r.PropReservationId!.Value)
            .ToListAsync(ct);

        var held = await HeldElsewhereAsync(from, to, itemIds, ownReservations, ct);

        /* ---------------- crew and vendors ---------------- */

        var crew = await Db.CrewMembers.AsNoTracking()
            .Where(c => c.Status == CrewStatuses.Active)
            .ToListAsync(ct);

        var clashes = await Db.CrewAssignments.AsNoTracking()
            .Where(a => CrewAssignmentStatuses.Blocking.Contains(a.Status))
            .Where(a => a.FromDate <= to && a.ToDate >= from)
            .Select(a => a.CrewMemberId)
            .ToListAsync(ct);

        var busy = clashes.ToHashSet();

        var coverage = CrewRoles.All
            .Select(role =>
            {
                var canWork = crew.Where(c => c.CanWork(role)).ToList();
                var free = canWork.Count(c => !busy.Contains(c.Id));
                var rates = canWork.Where(c => c.DayRate > 0).Select(c => c.DayRate!.Value).ToList();

                return new CrewRoleCoverageDto(
                    role, canWork.Count, free, canWork.Count - free,
                    rates.Count == 0 ? null : Math.Round(rates.Average(), 0));
            })
            .Where(r => r.OnRoster > 0)
            .OrderByDescending(r => r.Available)
            .ToList();

        var vendors = await Db.Vendors.AsNoTracking()
            .Include(v => v.Rates)
            .Include(v => v.Documents)
            .Where(v => v.Status == VendorStatuses.Active || v.Status == VendorStatuses.OnWatch)
            .OrderBy(v => v.Name)
            .Take(100)
            .ToListAsync(ct);

        var committed = await Db.VendorPurchaseOrders.AsNoTracking()
            .Where(o => PurchaseOrderStatuses.Blocking.Contains(o.Status))
            .Where(o => o.ServiceDate <= to && o.ServiceEndDate >= from)
            .GroupBy(o => o.VendorId)
            .Select(g => new { VendorId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.VendorId, x => x.Count, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return Ok(new QuotationResourceOptionsDto(
            from, to,
            kits.Select(k => KitDto(k, held)).ToList(),
            props.Select(p =>
            {
                var free = Math.Max(0, p.GoodQuantity - held.GetValueOrDefault(p.Id));
                return new PropAvailabilityDto(
                    p.Id, p.Name, p.Code, p.Category?.Name ?? "", p.Unit,
                    Thumb(p), p.GoodQuantity, held.GetValueOrDefault(p.Id), free, []);
            }).ToList(),
            coverage,
            vendors
                .Select(v => VendorsController.ToDto(v, today, committed.GetValueOrDefault(v.Id)))
                .ToList()));
    }

    /* ------------------------------------------------------------------ *
     * Building the plan
     * ------------------------------------------------------------------ */

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/resources")]
    public async Task<ActionResult<QuotationMarginDto>> AddResource(
        int id, QuotationResourceInput input, CancellationToken ct)
    {
        var quotation = await LoadEditableAsync(id, ct);
        var (from, to) = WindowFor(quotation);

        var kind = Require(input.ResourceKind, QuotationResourceKinds.All, "resource kind");
        var order = await NextOrderAsync(id, ct);

        var resource = kind switch
        {
            QuotationResourceKinds.Prop => await BuildPropAsync(quotation, input, from, to, ct),
            QuotationResourceKinds.Crew => await BuildCrewAsync(quotation, input, from, to, ct),
            QuotationResourceKinds.Vendor => await BuildVendorAsync(quotation, input, from, to, ct),
            _ => throw ApiException.BadRequest(
                "A kit goes on through the kit endpoint, which expands it into its items."),
        };

        resource.SortOrder = order;
        Db.QuotationResources.Add(resource);
        await Db.SaveChangesAsync(ct);

        return Ok(await BuildMarginAsync(id, ct));
    }

    /// <summary>
    /// Puts a whole kit on the proposal, one line per item.
    ///
    /// Expanded rather than kept as a single opaque line: a client asks what is
    /// in the mandap, and a coordinator needs the availability answered piece by
    /// piece. When <c>UseKitRate</c> is set the set is priced as one thing and
    /// the item lines carry the cost but no sell price, so the arithmetic still
    /// adds up without double-charging.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/resources/kit")]
    public async Task<ActionResult<QuotationMarginDto>> AddKit(
        int id, QuotationKitInput input, CancellationToken ct)
    {
        var quotation = await LoadEditableAsync(id, ct);
        var (from, to) = WindowFor(quotation);
        var days = DaysIn(from, to);

        var kit = await Db.PropKits.AsNoTracking()
            .Include(k => k.Lines).ThenInclude(l => l.Item)
            .FirstOrDefaultAsync(k => k.Id == input.PropKitId, ct)
            ?? throw ApiException.BadRequest("Select a valid kit.");

        var multiplier = Math.Max(1, input.Multiplier);

        var wanted = kit.Lines
            .Where(l => !input.EssentialOnly || !l.IsOptional)
            .Where(l => l.Item is not null && l.Item.Status == PropItemStatuses.Active)
            .OrderBy(l => l.SortOrder)
            .ToList();

        if (wanted.Count == 0)
            throw ApiException.BadRequest("That kit has no usable lines.");

        var order = await NextOrderAsync(id, ct);

        foreach (var line in wanted)
        {
            var item = line.Item!;

            Db.QuotationResources.Add(new QuotationResource
            {
                CompanyId = Db.Tenant.CompanyId,
                QuotationId = id,
                ResourceKind = QuotationResourceKinds.Prop,
                State = QuotationResourceStates.Planned,
                PropItemId = item.Id,
                // Recorded even on the expanded lines, so the plan can still say
                // these forty rows are one mandap.
                PropKitId = kit.Id,
                Description = $"{item.Name} ({kit.Name})",
                ChargeGroup = ChargeGroups.Decor,
                Quantity = line.Quantity * multiplier,
                QuantityUnit = item.Unit,
                Days = days,
                UnitCost = 0m,
                UnitSell = input.UseKitRate ? 0m : item.RentalRatePerDay ?? 0m,
                FromDate = from,
                ToDate = to,
                SortOrder = order++,
                Notes = line.IsOptional ? "Optional in the kit" : null,
            });
        }

        // The set's own price, carried on a single internal-facing line so the
        // client's copy reads "Traditional Mandap Set" once rather than forty
        // times at nought.
        if (input.UseKitRate && kit.RentalRatePerDay is decimal kitRate && kitRate > 0)
        {
            Db.QuotationResources.Add(new QuotationResource
            {
                CompanyId = Db.Tenant.CompanyId,
                QuotationId = id,
                ResourceKind = QuotationResourceKinds.PropKit,
                State = QuotationResourceStates.Planned,
                PropKitId = kit.Id,
                Description = kit.Name,
                ChargeGroup = ChargeGroups.Decor,
                Quantity = multiplier,
                QuantityUnit = "set",
                Days = days,
                UnitCost = 0m,
                UnitSell = kitRate,
                FromDate = from,
                ToDate = to,
                SortOrder = order++,
            });
        }

        await Db.SaveChangesAsync(ct);
        return Ok(await BuildMarginAsync(id, ct));
    }

    /// <summary>
    /// Quotes a headcount of a trade — "four bearers, three days" — without
    /// naming anybody.
    ///
    /// Nobody is booked at this point on purpose. A proposal that pencilled in
    /// four named people would hold them against a deal that has not closed,
    /// and the roster would fill up with ghosts. Names are picked at conversion,
    /// when the deal is real.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/resources/crew")]
    public async Task<ActionResult<QuotationMarginDto>> AddCrew(
        int id, QuotationCrewInput input, CancellationToken ct)
    {
        var quotation = await LoadEditableAsync(id, ct);
        var (from, to) = WindowFor(quotation);

        var role = Require(input.CrewRole, CrewRoles.All, "crew role");
        if (input.Headcount <= 0)
            throw ApiException.BadRequest("How many people?");

        var windowFrom = input.FromDate ?? from;
        var windowTo = input.ToDate ?? to;
        var days = input.Days ?? DaysIn(windowFrom, windowTo);

        // The going rate for this trade, so a coordinator quoting four bearers
        // does not have to look up what a bearer costs.
        var typical = await Db.CrewMembers.AsNoTracking()
            .Where(c => c.Status == CrewStatuses.Active && c.PrimaryRole == role)
            .Where(c => c.DayRate != null && c.DayRate > 0)
            .Select(c => c.DayRate!.Value)
            .ToListAsync(ct);

        var cost = input.UnitCost ?? (typical.Count == 0 ? 0m : Math.Round(typical.Average(), 0));

        Db.QuotationResources.Add(new QuotationResource
        {
            CompanyId = Db.Tenant.CompanyId,
            QuotationId = id,
            ResourceKind = QuotationResourceKinds.Crew,
            State = QuotationResourceStates.Planned,
            CrewRole = role,
            Description = $"{Humanise(role)} × {input.Headcount}",
            ChargeGroup = ChargeGroups.Logistics,
            Quantity = input.Headcount,
            QuantityUnit = "person",
            Days = Math.Max(1, days),
            UnitCost = cost,
            // Sold at a default markup when nobody says otherwise. Explicit
            // rather than hidden, so a desk that prices crew differently only
            // has to type the number once.
            UnitSell = input.UnitSell ?? Math.Round(cost * 1.4m, 0),
            FromDate = windowFrom,
            ToDate = windowTo,
            SortOrder = await NextOrderAsync(id, ct),
        });

        await Db.SaveChangesAsync(ct);
        return Ok(await BuildMarginAsync(id, ct));
    }

    private async Task<QuotationResource> BuildPropAsync(
        Quotation quotation,
        QuotationResourceInput input,
        DateOnly from,
        DateOnly to,
        CancellationToken ct)
    {
        if (input.PropItemId is not int itemId)
            throw ApiException.BadRequest("Which prop?");

        var item = await Db.PropItems.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == itemId, ct)
            ?? throw ApiException.BadRequest("Select a valid prop.");

        if (item.Status != PropItemStatuses.Active)
            throw ApiException.BadRequest(
                $"{item.Name} is {item.Status.ToLowerInvariant()} and cannot be quoted.");

        var windowFrom = input.FromDate ?? from;
        var windowTo = input.ToDate ?? to;

        return new QuotationResource
        {
            CompanyId = Db.Tenant.CompanyId,
            QuotationId = quotation.Id,
            ResourceKind = QuotationResourceKinds.Prop,
            State = QuotationResourceStates.Planned,
            PropItemId = item.Id,
            Description = input.Description ?? item.Name,
            ChargeGroup = input.ChargeGroup ?? ChargeGroups.Decor,
            Quantity = Math.Max(1, input.Quantity),
            QuantityUnit = input.QuantityUnit ?? item.Unit,
            Days = Math.Max(1, input.Days ?? DaysIn(windowFrom, windowTo)),
            // Owned stock costs nothing to send out; the piece is already
            // bought, and wear belongs in depreciation rather than on a line.
            UnitCost = input.UnitCost ?? 0m,
            UnitSell = input.UnitSell ?? item.RentalRatePerDay ?? 0m,
            TaxRate = input.TaxRate ?? 0.18m,
            FromDate = windowFrom,
            ToDate = windowTo,
            IsInternalOnly = input.IsInternalOnly,
            Notes = input.Notes,
        };
    }

    private async Task<QuotationResource> BuildCrewAsync(
        Quotation quotation,
        QuotationResourceInput input,
        DateOnly from,
        DateOnly to,
        CancellationToken ct)
    {
        var windowFrom = input.FromDate ?? from;
        var windowTo = input.ToDate ?? to;
        var days = Math.Max(1, input.Days ?? DaysIn(windowFrom, windowTo));

        CrewMember? member = null;
        if (input.CrewMemberId is int memberId)
        {
            member = await Db.CrewMembers.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == memberId, ct)
                ?? throw ApiException.BadRequest("Select a valid crew member.");
        }

        var role = input.CrewRole is null
            ? member?.PrimaryRole ?? CrewRoles.Helper
            : Require(input.CrewRole, CrewRoles.All, "crew role");

        var cost = input.UnitCost ?? member?.DayRate ?? 0m;

        return new QuotationResource
        {
            CompanyId = Db.Tenant.CompanyId,
            QuotationId = quotation.Id,
            ResourceKind = QuotationResourceKinds.Crew,
            State = QuotationResourceStates.Planned,
            CrewMemberId = member?.Id,
            CrewRole = role,
            Description = input.Description ?? member?.Name ?? Humanise(role),
            ChargeGroup = input.ChargeGroup ?? ChargeGroups.Logistics,
            Quantity = Math.Max(1, input.Quantity),
            QuantityUnit = input.QuantityUnit ?? "person",
            Days = days,
            UnitCost = cost,
            UnitSell = input.UnitSell ?? Math.Round(cost * 1.4m, 0),
            TaxRate = input.TaxRate ?? 0.18m,
            FromDate = windowFrom,
            ToDate = windowTo,
            IsInternalOnly = input.IsInternalOnly,
            Notes = input.Notes,
        };
    }

    private async Task<QuotationResource> BuildVendorAsync(
        Quotation quotation,
        QuotationResourceInput input,
        DateOnly from,
        DateOnly to,
        CancellationToken ct)
    {
        if (input.VendorId is not int vendorId)
            throw ApiException.BadRequest("Which vendor?");

        var vendor = await Db.Vendors.AsNoTracking()
            .Include(v => v.Rates)
            .FirstOrDefaultAsync(v => v.Id == vendorId, ct)
            ?? throw ApiException.BadRequest("Select a valid vendor.");

        if (!vendor.IsBookable)
            throw ApiException.BadRequest(
                $"{vendor.Name} is {vendor.Status.ToLowerInvariant()} and cannot be quoted.");

        var rate = input.VendorRateId is int rateId
            ? vendor.Rates.FirstOrDefault(r => r.Id == rateId)
                ?? throw ApiException.BadRequest("That rate is not on this vendor's card.")
            : null;

        var windowFrom = input.FromDate ?? from;
        var windowTo = input.ToDate ?? to;

        // A per-plate rate multiplies the head count the proposal was struck
        // on, which is the billed heads — guests or the venue's minimum,
        // whichever is higher — not the guest count alone.
        var quantity = input.Quantity > 1 || rate is null
            ? input.Quantity
            : rate.Basis == VendorRateBases.PerPlate
                ? Math.Max(1, quotation.BilledHeads)
                : 1m;

        // Only a per-day rate multiplies out across the window; a per-event
        // caterer charges once however long the setup takes.
        var days = input.Days
            ?? (rate?.Basis == VendorRateBases.PerDay ? DaysIn(windowFrom, windowTo) : 1);

        var cost = input.UnitCost ?? rate?.Rate ?? 0m;

        return new QuotationResource
        {
            CompanyId = Db.Tenant.CompanyId,
            QuotationId = quotation.Id,
            ResourceKind = QuotationResourceKinds.Vendor,
            State = QuotationResourceStates.Planned,
            VendorId = vendor.Id,
            VendorRateId = rate?.Id,
            Description = input.Description ?? rate?.Name ?? vendor.Name,
            ChargeGroup = input.ChargeGroup
                ?? GroupForService(rate?.Service ?? vendor.Services.Split(',').FirstOrDefault()),
            Quantity = Math.Max(1, quantity),
            QuantityUnit = input.QuantityUnit ?? BasisUnit(rate?.Basis),
            Days = Math.Max(1, days),
            UnitCost = cost,
            UnitSell = input.UnitSell ?? rate?.SellRate ?? cost,
            TaxRate = input.TaxRate ?? 0.18m,
            FromDate = windowFrom,
            ToDate = windowTo,
            IsInternalOnly = input.IsInternalOnly,
            Notes = input.Notes,
        };
    }

    /// <summary>Which banding a supplier's trade prints under.</summary>
    private static string GroupForService(string? service) => service?.Trim() switch
    {
        ServiceCategories.Catering => ChargeGroups.Catering,
        ServiceCategories.Decor => ChargeGroups.Decor,
        ServiceCategories.Photography or ServiceCategories.Videography => ChargeGroups.Photography,
        ServiceCategories.Entertainment or ServiceCategories.SoundAndLight
            or ServiceCategories.Choreography => ChargeGroups.Entertainment,
        ServiceCategories.Transport or ServiceCategories.Accommodation => ChargeGroups.Logistics,
        ServiceCategories.Venue => ChargeGroups.UnitCharge,
        _ => ChargeGroups.Catering,
    };

    private static string BasisUnit(string? basis) => basis switch
    {
        VendorRateBases.PerPlate => "plate",
        VendorRateBases.PerPiece => "piece",
        VendorRateBases.PerHour => "hour",
        VendorRateBases.PerSqft => "sqft",
        VendorRateBases.PerDay => "day",
        _ => "job",
    };

    [PermissionAction(ObjectAction.Edit)]
    [HttpPut("{id:int}/resources/{resourceId:int}")]
    public async Task<ActionResult<QuotationMarginDto>> UpdateResource(
        int id, int resourceId, QuotationResourceUpdate input, CancellationToken ct)
    {
        await LoadEditableAsync(id, ct);

        var resource = await Db.QuotationResources
            .FirstOrDefaultAsync(r => r.Id == resourceId && r.QuotationId == id, ct)
            ?? throw ApiException.NotFound("Resource line");

        if (resource.State == QuotationResourceStates.Converted)
            throw ApiException.BadRequest(
                "This line has already been booked. Change it on the gate pass or the order.");

        if (input.Quantity is decimal quantity) resource.Quantity = Math.Max(0, quantity);
        if (input.Days is int days) resource.Days = Math.Max(1, days);
        if (input.UnitCost is decimal cost) resource.UnitCost = Math.Max(0, cost);
        if (input.UnitSell is decimal sell) resource.UnitSell = Math.Max(0, sell);
        if (input.Description is not null) resource.Description = input.Description.Trim();
        if (input.ChargeGroup is not null) resource.ChargeGroup = input.ChargeGroup;
        if (input.IsInternalOnly is bool internalOnly) resource.IsInternalOnly = internalOnly;
        if (input.FromDate is DateOnly from) resource.FromDate = from;
        if (input.ToDate is DateOnly to) resource.ToDate = to;
        if (input.Notes is not null) resource.Notes = input.Notes;

        // A held line whose quantity or dates moved is holding the wrong thing,
        // so the hold follows it rather than silently going stale.
        if (resource.IsHolding && resource.PropReservationId is int reservationId)
        {
            var reservation = await Db.PropReservations
                .FirstOrDefaultAsync(r => r.Id == reservationId, ct);

            if (reservation is not null)
            {
                await GuardStockAsync(resource, reservation.Id, ct);

                reservation.Quantity = (int)Math.Ceiling(resource.Quantity);
                reservation.FromDate = resource.FromDate ?? reservation.FromDate;
                reservation.ToDate = resource.ToDate ?? reservation.ToDate;
            }
        }

        await Db.SaveChangesAsync(ct);
        return Ok(await BuildMarginAsync(id, ct));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpDelete("{id:int}/resources/{resourceId:int}")]
    public async Task<ActionResult<QuotationMarginDto>> RemoveResource(
        int id, int resourceId, CancellationToken ct)
    {
        await LoadEditableAsync(id, ct);

        var resource = await Db.QuotationResources
            .FirstOrDefaultAsync(r => r.Id == resourceId && r.QuotationId == id, ct)
            ?? throw ApiException.NotFound("Resource line");

        if (resource.State == QuotationResourceStates.Converted)
            throw ApiException.BadRequest(
                "This line has already been booked. Cancel the gate pass or the order instead.");

        await ReleaseAsync(resource, ct);

        Db.QuotationResources.Remove(resource);
        await Db.SaveChangesAsync(ct);

        return Ok(await BuildMarginAsync(id, ct));
    }

    /* ------------------------------------------------------------------ *
     * Holding
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Softly holds the proposal's stock while the client decides.
    ///
    /// The hold expires with the quotation's validity, so a deal that goes quiet
    /// hands the crates back on its own — nothing has to sweep it, because an
    /// expired reservation already reads as released everywhere. That is the
    /// whole reason <c>PropReservation.ExpiresAt</c> exists.
    ///
    /// Lines the godown cannot supply are skipped with a warning rather than
    /// failing the whole action: a proposal with one short line is still worth
    /// holding the other nine for.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/resources/hold")]
    public async Task<ActionResult<HoldResultDto>> Hold(
        int id, HoldResourcesInput input, CancellationToken ct)
    {
        var quotation = await Db.Quotations.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw ApiException.NotFound("Quotation");

        if (quotation.Status is QuotationStatuses.Rejected or QuotationStatuses.Expired)
            throw ApiException.BadRequest(
                $"A {quotation.Status.ToLowerInvariant()} proposal cannot hold stock.");

        var expiresAt = input.ExpiresAt ?? quotation.ValidUntil;
        if (expiresAt <= DateTime.UtcNow)
            throw ApiException.BadRequest(
                "That expiry is already past. Extend the proposal's validity first.");

        var resources = await Db.QuotationResources
            .Include(r => r.PropItem)
            .Where(r => r.QuotationId == id)
            .Where(r => r.PropItemId != null)
            .Where(r => r.State == QuotationResourceStates.Planned
                || r.State == QuotationResourceStates.Released)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct);

        var warnings = new List<string>();
        var held = 0;

        foreach (var resource in resources)
        {
            var item = resource.PropItem;
            if (item is null) continue;

            var wanted = (int)Math.Ceiling(resource.Quantity);
            if (wanted <= 0) continue;

            var free = await FreeForAsync(resource, null, ct);
            if (wanted > free)
            {
                warnings.Add(
                    $"{item.Name}: {wanted} wanted, {Math.Max(0, free)} free — not held.");
                continue;
            }

            var reservation = new PropReservation
            {
                CompanyId = quotation.CompanyId,
                PropItemId = item.Id,
                QuotationId = quotation.Id,
                LeadId = quotation.LeadId,
                Quantity = wanted,
                FromDate = resource.FromDate ?? DateOnly.FromDateTime(quotation.ValidUntil),
                ToDate = resource.ToDate ?? DateOnly.FromDateTime(quotation.ValidUntil).AddDays(1),
                Status = PropIssueStatuses.Reserved,
                ExpiresAt = expiresAt,
                EventName = quotation.Title,
                ClientName = quotation.CustomerName,
                Notes = $"Soft hold for {quotation.QuoteNumber}",
            };

            Db.PropReservations.Add(reservation);
            await Db.SaveChangesAsync(ct);

            resource.PropReservationId = reservation.Id;
            resource.State = QuotationResourceStates.Held;
            held++;
        }

        await Db.SaveChangesAsync(ct);

        return Ok(new HoldResultDto(
            held, resources.Count - held, warnings, await BuildMarginAsync(id, ct)));
    }

    /// <summary>Hands every hold back, without touching the priced plan.</summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/resources/release")]
    public async Task<ActionResult<QuotationMarginDto>> Release(int id, CancellationToken ct)
    {
        var resources = await Db.QuotationResources
            .Where(r => r.QuotationId == id && r.State == QuotationResourceStates.Held)
            .ToListAsync(ct);

        foreach (var resource in resources)
        {
            await ReleaseAsync(resource, ct);
        }

        await Db.SaveChangesAsync(ct);
        return Ok(await BuildMarginAsync(id, ct));
    }

    /// <summary>Drops one line's hold and marks it released.</summary>
    private async Task ReleaseAsync(QuotationResource resource, CancellationToken ct)
    {
        if (resource.PropReservationId is int reservationId)
        {
            var reservation = await Db.PropReservations
                .FirstOrDefaultAsync(r => r.Id == reservationId, ct);

            if (reservation is not null)
            {
                Db.PropReservations.Remove(reservation);
            }

            resource.PropReservationId = null;
        }

        if (resource.State == QuotationResourceStates.Held)
        {
            resource.State = QuotationResourceStates.Released;
        }
    }

    /// <summary>
    /// What the godown can supply for one line, over its own window.
    ///
    /// The line's own hold is excluded, so raising a held line from ten to
    /// twelve is measured against twelve free rather than against two.
    /// </summary>
    private async Task<int> FreeForAsync(
        QuotationResource resource, int? alsoIgnore, CancellationToken ct)
    {
        if (resource.PropItemId is not int itemId) return int.MaxValue;

        var item = resource.PropItem
            ?? await Db.PropItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == itemId, ct);

        if (item is null) return 0;

        var from = resource.FromDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var to = resource.ToDate ?? from;

        var ignore = new List<int>();
        if (resource.PropReservationId is int own) ignore.Add(own);
        if (alsoIgnore is int extra) ignore.Add(extra);

        var held = await HeldElsewhereAsync(from, to, [itemId], ignore, ct);
        return item.GoodQuantity - held.GetValueOrDefault(itemId);
    }

    private async Task GuardStockAsync(
        QuotationResource resource, int ignoreReservationId, CancellationToken ct)
    {
        var free = await FreeForAsync(resource, ignoreReservationId, ct);
        var wanted = (int)Math.Ceiling(resource.Quantity);

        if (wanted > free)
        {
            throw ApiException.BadRequest(
                $"{resource.Description}: only {Math.Max(0, free)} free over these dates.");
        }
    }

    /* ------------------------------------------------------------------ *
     * Conversion
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Turns an accepted proposal into the real bookings.
    ///
    /// One gate pass for all the props, one crew assignment per person, and one
    /// purchase order per supplier — grouped that way because that is how each
    /// is worked: the godown loads one truck, a coordinator rings people one at
    /// a time, and a caterer wants one order rather than four.
    ///
    /// Idempotent by line: anything already converted is skipped, so running it
    /// twice after a partial failure finishes the job rather than duplicating it.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/resources/convert")]
    public async Task<ActionResult<ConversionResultDto>> Convert(
        int id, ConvertQuotationInput input, CancellationToken ct)
    {
        var quotation = await Db.Quotations.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw ApiException.NotFound("Quotation");

        if (quotation.Status is not QuotationStatuses.Accepted)
            throw ApiException.BadRequest(
                "Only an accepted proposal can be converted. Accept it first.");

        var resources = await Base()
            .Where(r => r.QuotationId == id)
            .Where(r => r.State != QuotationResourceStates.Converted)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct);

        if (resources.Count == 0)
            throw ApiException.BadRequest("There is nothing left on this plan to convert.");

        var (defaultFrom, defaultTo) = WindowFor(quotation);
        var dispatch = input.DispatchDate ?? defaultFrom;
        var back = input.ReturnDate ?? defaultTo;

        if (back < dispatch)
            throw ApiException.BadRequest("The return date cannot fall before the dispatch date.");

        var warnings = new List<string>();

        /* ---------------- props → one gate pass ---------------- */

        PropIssue? issue = null;
        var propPieces = 0;
        var propLines = 0;

        var propResources = input.IncludeProps
            ? resources.Where(r => r.PropItemId is not null).ToList()
            : [];

        if (propResources.Count > 0)
        {
            issue = new PropIssue
            {
                CompanyId = quotation.CompanyId,
                Code = Code("GP"),
                Status = PropIssueStatuses.Draft,
                LeadId = quotation.LeadId,
                QuotationId = quotation.Id,
                ProjectId = quotation.ProjectId,
                EventName = quotation.Title,
                EventType = quotation.EventType,
                ClientName = quotation.CustomerName,
                VenueName = quotation.ProjectId is null ? null : quotation.UnitNumber,
                DispatchDate = dispatch,
                EventDate = quotation.EventDate is DateTime d ? DateOnly.FromDateTime(d) : null,
                ExpectedReturnDate = back,
                StoreId = input.StoreId,
                OwnerId = quotation.OwnerId,
                Notes = $"From {quotation.QuoteNumber}",
            };

            Db.PropIssues.Add(issue);
            await Db.SaveChangesAsync(ct);

            var order = 0;

            foreach (var resource in propResources)
            {
                var quantity = (int)Math.Ceiling(resource.Quantity);
                if (quantity <= 0) continue;

                // The proposal's own soft hold is dropped first, so the gate
                // pass's own reservation is not measured against it.
                await ReleaseAsync(resource, ct);
                await Db.SaveChangesAsync(ct);

                var item = await Db.PropItems.FirstOrDefaultAsync(i => i.Id == resource.PropItemId, ct);
                if (item is null)
                {
                    warnings.Add($"{resource.Description}: the item no longer exists — skipped.");
                    continue;
                }

                var held = await HeldElsewhereAsync(dispatch, back, [item.Id], [], ct);
                var free = item.GoodQuantity - held.GetValueOrDefault(item.Id);

                if (quantity > free)
                {
                    warnings.Add(
                        $"{item.Name}: {quantity} quoted, only {Math.Max(0, free)} free — " +
                        "left off the gate pass.");
                    continue;
                }

                var existing = issue.Lines.FirstOrDefault(l => l.PropItemId == item.Id);
                if (existing is not null)
                {
                    existing.ReservedQuantity += quantity;
                }
                else
                {
                    issue.Lines.Add(new PropIssueLine
                    {
                        PropIssueId = issue.Id,
                        PropItemId = item.Id,
                        ReservedQuantity = quantity,
                        RatePerDay = resource.UnitSell,
                        ChargeableDays = Math.Max(1, resource.Days),
                        Notes = resource.Notes,
                        SortOrder = order++,
                    });
                    propLines++;
                }

                Db.PropReservations.Add(new PropReservation
                {
                    CompanyId = quotation.CompanyId,
                    PropItemId = item.Id,
                    PropIssueId = issue.Id,
                    QuotationId = quotation.Id,
                    LeadId = quotation.LeadId,
                    Quantity = quantity,
                    FromDate = dispatch,
                    ToDate = back.AddDays(item.TurnaroundDays),
                    Status = PropIssueStatuses.Reserved,
                    EventName = quotation.Title,
                    ClientName = quotation.CustomerName,
                });

                resource.State = QuotationResourceStates.Converted;
                resource.PropIssueId = issue.Id;
                propPieces += quantity;
            }

            issue.Status = PropIssueStatuses.Reserved;
            await Db.SaveChangesAsync(ct);
        }

        /* ---------------- crew → assignments ---------------- */

        var assignmentIds = new List<int>();

        if (input.IncludeCrew)
        {
            foreach (var resource in resources.Where(r => r.CrewRole is not null))
            {
                var role = resource.CrewRole!;
                var from = resource.FromDate ?? dispatch;
                var to = resource.ToDate ?? back;
                var wanted = (int)Math.Ceiling(resource.Quantity);

                var picked = await PickCrewAsync(resource, role, from, to, wanted, ct);

                if (picked.Count < wanted)
                {
                    warnings.Add(
                        $"{Humanise(role)}: {wanted} quoted, only {picked.Count} free " +
                        $"between {from:dd MMM} and {to:dd MMM}.");
                }

                foreach (var member in picked)
                {
                    var assignment = new CrewAssignment
                    {
                        CompanyId = quotation.CompanyId,
                        CrewMemberId = member.Id,
                        Status = CrewAssignmentStatuses.Planned,
                        Role = role,
                        LeadId = quotation.LeadId,
                        ProjectId = quotation.ProjectId,
                        PropIssueId = issue?.Id,
                        EventName = quotation.Title,
                        ClientName = quotation.CustomerName,
                        FromDate = from,
                        ToDate = to,
                        DayRate = resource.UnitCost > 0 ? resource.UnitCost : member.DayRate,
                        Notes = $"From {quotation.QuoteNumber}",
                    };

                    Db.CrewAssignments.Add(assignment);
                    await Db.SaveChangesAsync(ct);

                    assignmentIds.Add(assignment.Id);
                    resource.CrewAssignmentId ??= assignment.Id;
                }

                if (picked.Count > 0)
                {
                    resource.State = QuotationResourceStates.Converted;
                }
            }
        }

        /* ---------------- vendors → one order each ---------------- */

        var orderIds = new List<int>();
        var orderCodes = new List<string>();
        var vendorLines = 0;

        if (input.IncludeVendors)
        {
            var byVendor = resources
                .Where(r => r.VendorId is not null)
                .GroupBy(r => r.VendorId!.Value);

            foreach (var group in byVendor)
            {
                var vendor = await Db.Vendors.FirstOrDefaultAsync(v => v.Id == group.Key, ct);
                if (vendor is null)
                {
                    warnings.Add("A supplier on this plan no longer exists — skipped.");
                    continue;
                }

                var committed = await Db.VendorPurchaseOrders
                    .CountAsync(o => o.VendorId == vendor.Id
                        && PurchaseOrderStatuses.Blocking.Contains(o.Status)
                        && o.ServiceDate <= back && o.ServiceEndDate >= dispatch, ct);

                if (committed >= vendor.ConcurrentEventCapacity)
                {
                    warnings.Add(
                        $"{vendor.Name} already has {committed} job(s) over these dates and can " +
                        $"run {vendor.ConcurrentEventCapacity} — order raised as a draft, not confirmed.");
                }

                var lines = group.OrderBy(r => r.SortOrder).ToList();

                var order = new VendorPurchaseOrder
                {
                    CompanyId = quotation.CompanyId,
                    Code = Code("PO"),
                    VendorId = vendor.Id,
                    Status = PurchaseOrderStatuses.Draft,
                    Service = ServiceFor(lines[0]),
                    LeadId = quotation.LeadId,
                    QuotationId = quotation.Id,
                    ProjectId = quotation.ProjectId,
                    EventName = quotation.Title,
                    EventType = quotation.EventType,
                    ClientName = quotation.CustomerName,
                    GuestCount = quotation.GuestCount,
                    ServiceDate = lines.Min(l => l.FromDate) ?? dispatch,
                    ServiceEndDate = lines.Max(l => l.ToDate) ?? back,
                    OwnerId = quotation.OwnerId,
                    Notes = $"From {quotation.QuoteNumber}",
                };

                var sortOrder = 0;
                foreach (var line in lines)
                {
                    order.Lines.Add(new VendorPoLine
                    {
                        VendorRateId = line.VendorRateId,
                        Description = line.Description,
                        Basis = BasisFor(line),
                        Quantity = line.Quantity * Math.Max(1, line.Days),
                        Rate = line.UnitCost,
                        SellRate = line.UnitSell,
                        Notes = line.Notes,
                        SortOrder = sortOrder++,
                    });
                    vendorLines++;
                }

                order.TotalCost = order.Lines.Sum(l => l.LineCost);
                order.TotalSell = order.Lines.Sum(l => l.LineSell);

                Db.VendorPurchaseOrders.Add(order);
                await Db.SaveChangesAsync(ct);

                orderIds.Add(order.Id);
                orderCodes.Add(order.Code);

                foreach (var line in lines)
                {
                    line.State = QuotationResourceStates.Converted;
                    line.VendorPurchaseOrderId = order.Id;
                }
            }
        }

        await LogLeadActivityAsync(
            quotation.LeadId, LeadActivityTypes.Note,
            $"{quotation.QuoteNumber} converted — "
            + $"{propPieces} pieces on {issue?.Code ?? "no gate pass"}, "
            + $"{assignmentIds.Count} crew, {orderIds.Count} supplier order(s).", ct);

        await Db.SaveChangesAsync(ct);

        var margin = await BuildMarginAsync(id, ct);

        return Ok(new ConversionResultDto(
            id, issue?.Id, issue?.Code, propLines, propPieces,
            assignmentIds, assignmentIds.Count,
            orderIds, orderCodes, vendorLines,
            margin.CommittedCost, warnings));
    }

    /// <summary>
    /// Picks people for a quoted headcount.
    ///
    /// Ordered by reliability rather than by name: the coordinator would have
    /// picked the ones who turn up, and an automatic booking that hands them the
    /// worst four would be worse than useless. A line that already names
    /// somebody takes that person and nobody else.
    /// </summary>
    private async Task<List<CrewMember>> PickCrewAsync(
        QuotationResource resource,
        string role,
        DateOnly from,
        DateOnly to,
        int wanted,
        CancellationToken ct)
    {
        var busy = await Db.CrewAssignments.AsNoTracking()
            .Where(a => CrewAssignmentStatuses.Blocking.Contains(a.Status))
            .Where(a => a.FromDate <= to && a.ToDate >= from)
            .Select(a => a.CrewMemberId)
            .ToListAsync(ct);

        var taken = busy.ToHashSet();

        if (resource.CrewMemberId is int named)
        {
            var member = await Db.CrewMembers.FirstOrDefaultAsync(c => c.Id == named, ct);
            return member is not null && member.IsBookable && !taken.Contains(member.Id)
                ? [member]
                : [];
        }

        var candidates = await Db.CrewMembers
            .Where(c => c.Status == CrewStatuses.Active)
            .ToListAsync(ct);

        return candidates
            .Where(c => c.CanWork(role) && !taken.Contains(c.Id))
            .OrderBy(c => c.NoShowCount)
            .ThenByDescending(c => c.Rating ?? 0)
            .ThenBy(c => c.DayRate ?? 0)
            .Take(Math.Max(0, wanted))
            .ToList();
    }

    private static string ServiceFor(QuotationResource resource) =>
        resource.VendorRate?.Service ?? ServiceCategories.Decor;

    private static string BasisFor(QuotationResource resource) =>
        resource.VendorRate?.Basis ?? VendorRateBases.PerEvent;

    /* ------------------------------------------------------------------ *
     * Margin
     * ------------------------------------------------------------------ */

    /// <summary>
    /// What the proposal earns once delivering it is paid for.
    ///
    /// Planned cost comes off the resource plan; committed cost comes off what
    /// has actually been booked against the event. They are reported separately
    /// rather than reconciled, because the gap between them — a caterer
    /// confirmed at more than was quoted — is the number worth watching, and
    /// averaging it away would hide exactly the thing a manager opens this for.
    /// </summary>
    private async Task<QuotationMarginDto> BuildMarginAsync(int id, CancellationToken ct)
    {
        var quotation = await Db.Quotations.AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw ApiException.NotFound("Quotation");

        var resources = await Base().AsNoTracking()
            .Where(r => r.QuotationId == id)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct);

        /* ---------------- availability on the stock-backed lines ---------------- */

        var itemIds = resources.Select(r => r.PropItemId).OfType<int>().Distinct().ToList();
        var ownReservations = resources.Select(r => r.PropReservationId).OfType<int>().ToList();

        var (windowFrom, windowTo) = WindowFor(quotation);
        var held = itemIds.Count == 0
            ? []
            : await HeldElsewhereAsync(
                resources.Min(r => r.FromDate) ?? windowFrom,
                resources.Max(r => r.ToDate) ?? windowTo,
                itemIds, ownReservations, ct);

        var rows = resources.Select(r => ToDto(r, held)).ToList();

        /* ---------------- committed, from the real bookings ---------------- */

        var committed = 0m;

        if (quotation.LeadId is int leadId)
        {
            var orders = await Db.VendorPurchaseOrders.AsNoTracking()
                .Where(o => o.LeadId == leadId && o.Status != PurchaseOrderStatuses.Cancelled)
                .SumAsync(o => (decimal?)o.TotalCost, ct) ?? 0m;

            var assignments = await Db.CrewAssignments.AsNoTracking()
                .Include(a => a.CrewMember)
                .Where(a => a.LeadId == leadId && a.Status != CrewAssignmentStatuses.Cancelled)
                .ToListAsync(ct);

            committed = orders + assignments.Sum(a => a.TotalCost);
        }
        else
        {
            // No lead means nothing can be booked against it yet, so the only
            // honest committed figure is what this proposal itself created.
            var orderIds = resources.Select(r => r.VendorPurchaseOrderId).OfType<int>().Distinct().ToList();

            if (orderIds.Count > 0)
            {
                committed = await Db.VendorPurchaseOrders.AsNoTracking()
                    .Where(o => orderIds.Contains(o.Id))
                    .SumAsync(o => (decimal?)o.TotalCost, ct) ?? 0m;
            }
        }

        var plannedCost = rows.Sum(r => r.LineCost);
        var revenue = quotation.GrandTotal;
        var revenueExTax = quotation.Subtotal - quotation.DiscountAmount + quotation.ChargesBasic;

        var byGroup = rows
            .GroupBy(r => r.ChargeGroup)
            .Select(g =>
            {
                var sell = g.Sum(r => r.LineSell);
                var cost = g.Sum(r => r.LineCost);
                return new MarginByGroupDto(
                    g.Key, ChargeGroups.Label(g.Key), sell, cost, sell - cost,
                    sell == 0 ? null : Math.Round((sell - cost) / sell, 4),
                    g.Count());
            })
            .OrderByDescending(g => g.Cost)
            .ToList();

        return new QuotationMarginDto(
            quotation.Id, quotation.QuoteNumber, quotation.Status, quotation.CustomerName,
            quotation.EventType, quotation.EventDate, quotation.GuestCount,
            revenue, revenueExTax,
            plannedCost,
            revenue - plannedCost,
            revenue == 0 ? null : Math.Round((revenue - plannedCost) / revenue, 4),
            committed,
            revenue - committed,
            revenue == 0 ? null : Math.Round((revenue - committed) / revenue, 4),
            committed - plannedCost,
            rows.Count,
            rows.Count(r => r.State == QuotationResourceStates.Held),
            rows.Count(r => r.State == QuotationResourceStates.Converted),
            rows.Count(r => r.IsShort == true),
            byGroup, rows);
    }

    /* ------------------------------------------------------------------ *
     * Plumbing
     * ------------------------------------------------------------------ */

    private async Task<Quotation> LoadEditableAsync(int id, CancellationToken ct)
    {
        var quotation = await Db.Quotations.FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw ApiException.NotFound("Quotation");

        if (quotation.Status is QuotationStatuses.Rejected or QuotationStatuses.Expired)
            throw ApiException.BadRequest(
                $"A {quotation.Status.ToLowerInvariant()} proposal cannot be re-planned.");

        return quotation;
    }

    private async Task<int> NextOrderAsync(int quotationId, CancellationToken ct)
    {
        var max = await Db.QuotationResources
            .Where(r => r.QuotationId == quotationId)
            .MaxAsync(r => (int?)r.SortOrder, ct);

        return (max ?? -1) + 1;
    }

    private static string? Thumb(PropItem item) =>
        item.Photos.OrderBy(p => p.SortOrder).FirstOrDefault() is { } photo
            ? photo.ThumbnailUrl ?? photo.Url
            : null;

    private static PropKitDto KitDto(PropKit kit, Dictionary<int, int> held)
    {
        var lines = kit.Lines.OrderBy(l => l.SortOrder).Select(l =>
        {
            var item = l.Item;
            int? free = null;
            bool? isShort = null;

            if (item is not null)
            {
                free = Math.Max(0, item.GoodQuantity - held.GetValueOrDefault(item.Id));
                isShort = free < l.Quantity;
            }

            return new PropKitLineDto(
                l.Id, l.PropItemId, item?.Name ?? "", item?.Code ?? "",
                item?.Category?.Name ?? "", item?.Unit ?? "PCS",
                item is null ? null : Thumb(item),
                l.Quantity, l.IsOptional, l.Notes, l.SortOrder,
                item?.GoodQuantity ?? 0, free, isShort);
        }).ToList();

        var shortEssential = lines.Count(l =>
            l.IsShort == true && !kit.Lines.First(k => k.Id == l.Id).IsOptional);

        return new PropKitDto(
            kit.Id, kit.Name, kit.Code, kit.Description, kit.SetupType, kit.EventType,
            kit.CoverImageUrl, kit.RentalRatePerDay,
            kit.Lines.Sum(l => (l.Item?.RentalRatePerDay ?? 0) * l.Quantity),
            kit.SetupHours, kit.CrewRequired, kit.IsActive,
            lines.Count, lines.Sum(l => l.Quantity),
            shortEssential, shortEssential == 0,
            lines, kit.CreatedAt);
    }

    private static QuotationResourceDto ToDto(
        QuotationResource r, Dictionary<int, int> held)
    {
        int? free = null;
        bool? isShort = null;

        // A converted line's stock question is settled — the pieces are on a
        // gate pass, and that pass's own reservation is what the availability
        // sum would now be measuring them against. Reporting it as short would
        // flag every successfully booked line the moment it succeeded.
        if (r.PropItem is not null && r.State != QuotationResourceStates.Converted)
        {
            free = Math.Max(0, r.PropItem.GoodQuantity - held.GetValueOrDefault(r.PropItem.Id));
            isShort = free < r.Quantity;
        }

        return new QuotationResourceDto(
            r.Id, r.QuotationId, r.ResourceKind, r.State,
            r.PropItemId, r.PropKitId,
            r.CrewMemberId, r.CrewMember?.Name, r.CrewRole,
            r.VendorId, r.Vendor?.Name, r.VendorRateId,
            r.Description, r.ChargeGroup, r.IsInternalOnly, r.SortOrder, r.Notes,
            r.Quantity, r.QuantityUnit, r.Days,
            r.UnitCost, r.UnitSell, r.TaxRate,
            r.LineCost, r.LineSell, r.LineMargin, r.MarginFraction,
            r.FromDate, r.ToDate,
            r.PropItem is null ? null : Thumb(r.PropItem),
            free, isShort,
            r.PropReservationId, r.PropIssueId, r.CrewAssignmentId, r.VendorPurchaseOrderId,
            r.CreatedAt);
    }
}
