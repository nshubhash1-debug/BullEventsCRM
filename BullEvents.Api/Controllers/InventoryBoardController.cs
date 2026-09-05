using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using BullEvents.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// The inventory board and the moves made on it.
///
/// Split from <see cref="InventoryController"/>, which owns the CRUD and the
/// filterable unit list: this is the sales-floor view of the same stock, and it
/// carries the workflow — who may take a unit off the market, and who has to
/// agree.
/// </summary>
[ApiController]
[Route("api/inventory")]
[Authorize]
[SecuredBy(SecuredObjects.Unit)]
public class InventoryBoardController(
    AppDbContext db,
    ApprovalService approvals,
    SpaceBookingService spaceBookings)
    : CrmControllerBase(db)
{
    /// <summary>
    /// A hold that has run out is available again, reported rather than swept.
    /// Mirrors <see cref="InventoryController"/>: a nightly job would leave a
    /// window where the board and the truth disagree.
    /// </summary>
    private static string EffectiveStatus(Unit unit) =>
        unit.Status == UnitStatuses.Held && unit.HeldUntil < DateTime.UtcNow
            ? UnitStatuses.Available
            : unit.Status;

    /// <summary>Position on the floor, from the unit number's last two digits.</summary>
    private static int PositionOf(Unit unit)
    {
        var digits = new string(unit.UnitNumber.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var value) ? Math.Max(1, value % 100) : 1;
    }

    /* ------------------------------------------------------------------ *
     * Board
     * ------------------------------------------------------------------ */

    /// <summary>
    /// One payload for all three view modes — tiles, list and block grid.
    ///
    /// Pass <paramref name="eventDate"/> (and optionally <paramref name="slot"/>)
    /// to colour each space from <see cref="SpaceBooking"/> for that session —
    /// the question a banquet desk actually asks.
    /// </summary>
    [HttpGet("projects/{projectId:int}/board")]
    public async Task<ActionResult<InventoryBoardDto>> Board(
        int projectId,
        [FromQuery] int? towerId,
        [FromQuery] DateOnly? eventDate,
        [FromQuery] string? slot,
        CancellationToken ct)
    {
        var project = await Db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw ApiException.NotFound("Project");

        var towers = await Db.Towers.AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .OrderBy(t => t.Name)
            .Select(t => new TowerDto(
                t.Id, t.Name, t.FloorCount, t.UnitsPerFloor, t.Status,
                t.Units.Count(u => !u.IsDeleted)))
            .ToListAsync(ct);

        var tower = towerId is int id
            ? await Db.Towers.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct)
            : null;

        var units = await Db.Units.AsNoTracking()
            .Where(u => u.ProjectId == projectId && (towerId == null || u.TowerId == towerId))
            .ToListAsync(ct);

        var rateCards = await ActiveRateCardsAsync(projectId, towerId, ct);

        // Names for whoever is holding stock, resolved in one query rather than
        // per tile — a full venue is a few hundred of them.
        var holderIds = units.Where(u => u.HeldByUserId != null)
            .Select(u => u.HeldByUserId!.Value).Distinct().ToList();

        var holders = holderIds.Count == 0
            ? []
            : await Db.Users.Where(u => holderIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        var unitIds = units.Select(u => u.Id).ToList();
        var pending = await Db.Approvals
            .Where(a => a.EntityType == ApprovalEntities.Unit
                && a.Status == ApprovalStatuses.Pending
                && unitIds.Contains(a.EntityId))
            .ToDictionaryAsync(a => a.EntityId, a => a.Id, ct);

        var day = eventDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var session = string.IsNullOrWhiteSpace(slot) ? EventSlots.Evening : slot;

        var dayBookings = await Db.SpaceBookings.AsNoTracking()
            .Where(b => b.ProjectId == projectId && b.EventDate == day)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var bookingByUnit = dayBookings
            .Where(b => b.IsBlocking(now) && EventSlots.Overlaps(b.Slot, session))
            .GroupBy(b => b.UnitId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(b => b.Status == UnitStatuses.Booked || b.Status == UnitStatuses.Sold)
                    .ThenByDescending(b => b.Id)
                    .First());

        var boardUnits = units
            .Select(u => ToBoardUnit(
                u,
                CardFor(rateCards, u),
                holders,
                pending,
                bookingByUnit.GetValueOrDefault(u.Id),
                dateMode: true))
            .ToList();

        var floors = boardUnits
            .GroupBy(u => u.Floor)
            .OrderByDescending(g => g.Key)
            .Select(g => new InventoryFloorDto(
                g.Key,
                g.Count(u => u.Status == UnitStatuses.Available),
                g.Count(),
                g.OrderBy(u => u.Position).ToList()))
            .ToList();

        return Ok(new InventoryBoardDto(
            project.Id,
            project.Name,
            project.Developer,
            project.Address,
            project.ReraNumber,
            tower?.Id,
            tower?.Name,
            tower?.FloorCount ?? (units.Count == 0 ? 0 : units.Max(u => u.Floor)),
            tower?.UnitsPerFloor ?? 0,
            HeadlineCard(rateCards) is RateCard headline ? ToRateCard(headline, true) : null,
            rateCards.Select(c => ToRateCard(c, true)).ToList(),
            BuildStats(boardUnits),
            floors,
            towers));
    }

    private static InventoryStatsDto BuildStats(IReadOnlyList<BoardUnitDto> units)
    {
        int Count(string status) => units.Count(u => u.Status == status);

        decimal Value(params string[] statuses) => units
            .Where(u => statuses.Contains(u.Status))
            .Sum(u => u.TotalPrice);

        var committed = Count(UnitStatuses.Booked) + Count(UnitStatuses.Sold);

        return new InventoryStatsDto(
            units.Count,
            Count(UnitStatuses.Available),
            Count(UnitStatuses.Held),
            Count(UnitStatuses.Blocked),
            Count(UnitStatuses.Booked),
            Count(UnitStatuses.Sold),
            Count(UnitStatuses.NotForSale),
            Value(UnitStatuses.Available),
            Value(UnitStatuses.Booked, UnitStatuses.Sold),
            units.Sum(u => u.TotalPrice),
            units.Count == 0 ? 0 : decimal.Round(committed * 100m / units.Count, 1));
    }

    private static BoardUnitDto ToBoardUnit(
        Unit unit,
        RateCard? rateCard,
        Dictionary<int, string> holders,
        Dictionary<int, int> pending,
        SpaceBooking? booking,
        bool dateMode)
    {
        // The board prices at today's card so a rep reading a tile is quoting
        // the same number the quotation will produce. The unit's own stored
        // price is only a launch-time fallback.
        var rate = rateCard?.RatePerSqft ?? unit.PricePerSqft;
        var effective = rate + unit.PlcPerSqft;
        var area = unit.SuperArea ?? unit.BuiltUpArea ?? unit.CarpetArea;

        var catalogue = EffectiveStatus(unit);
        string status;
        string? customerName = unit.CustomerName;
        string? holdReason = unit.HoldReason;
        DateTime? heldUntil = unit.HeldUntil;

        if (catalogue is UnitStatuses.NotForSale or UnitStatuses.Blocked)
        {
            status = catalogue;
        }
        else if (dateMode)
        {
            // Catalogue Booked/Sold from the realty era must not paint a hall
            // red on every future date — only SpaceBooking answers the day.
            if (booking is not null)
            {
                status = booking.Status is UnitStatuses.Sold ? UnitStatuses.Sold : booking.Status;
                customerName = booking.ClientName ?? customerName;
                holdReason = booking.Notes ?? holdReason;
                heldUntil = booking.HoldExpiresAt ?? heldUntil;
            }
            else
            {
                status = UnitStatuses.Available;
                customerName = null;
                holdReason = null;
                heldUntil = null;
            }
        }
        else
        {
            status = catalogue;
        }

        return new BoardUnitDto(
            unit.Id,
            unit.UnitNumber,
            unit.Floor,
            PositionOf(unit),
            unit.Configuration,
            unit.CarpetArea,
            unit.BuiltUpArea,
            unit.SuperArea,
            unit.PlcPerSqft,
            rate,
            effective,
            unit.PricePerPlate > 0
                ? decimal.Round(
                    unit.BasePrice
                    + (unit.PricePerPlate * Math.Max(Math.Max(unit.MinimumPlates, unit.SeatingCapacity), 1)),
                    2)
                : decimal.Round(area * effective, 2),
            status,
            unit.IsCornerUnit,
            unit.Facing,
            unit.HeldByUserId,
            unit.HeldByUserId is int held ? holders.GetValueOrDefault(held) : null,
            heldUntil,
            holdReason,
            unit.BookedByContactId,
            unit.BookedByLeadId ?? booking?.LeadId,
            customerName,
            unit.CustomerPhone,
            unit.SalesPersonId,
            unit.SalesPersonName,
            unit.BookedAt,
            unit.BookedQuotationId ?? booking?.QuotationId,
            unit.BlockReason,
            pending.TryGetValue(unit.Id, out var approvalId) ? approvalId : null);
    }

    /* ------------------------------------------------------------------ *
     * Rate cards
     * ------------------------------------------------------------------ */

    /// <summary>
    /// The card in force today: the newest one that has already started.
    /// Tower-specific and type-specific cards win over the project-wide default.
    /// </summary>
    /// <summary>
    /// Every card in force on the board's date, most specific first.
    ///
    /// A project may price by configuration — a mall's ground-floor retail and
    /// its food court are separate cards effective the same morning — so the
    /// board cannot resolve one card and apply it to every tile. It loads the
    /// set and each tile picks its own, the same way the quotation engine does.
    /// One card for the whole project still works: it simply carries no unit
    /// type and matches everything.
    /// </summary>
    private async Task<List<RateCard>> ActiveRateCardsAsync(
        int projectId, int? towerId, CancellationToken ct, DateTime? on = null)
    {
        var asOf = (on ?? DateTime.UtcNow).Date;

        var effective = await Db.RateCards.AsNoTracking()
            .Where(c => c.ProjectId == projectId
                && c.EffectiveFrom <= asOf
                && (c.TowerId == null || c.TowerId == towerId))
            .OrderByDescending(c => c.EffectiveFrom)
            .ThenByDescending(c => c.TowerId == null ? 0 : 1)
            .ThenByDescending(c => c.UnitType == null ? 0 : 1)
            .ToListAsync(ct);

        // Collapsed to the newest card per configuration. Cards are dated
        // rather than replaced — July's card is still "effective on or before
        // today" long after August supersedes it — so without this the set
        // carries a project's whole pricing history and the header reports a
        // spread between a rate in force and one that was retired.
        return effective
            .GroupBy(c => c.UnitType)
            .Select(g => g.First())
            .ToList();
    }

    /// <summary>
    /// The card that applies to one unit: its own configuration's card if the
    /// project prices that way, otherwise the project-wide one. Null when the
    /// project has no card in force, and the tile falls back to the unit's
    /// stored launch price.
    /// </summary>
    private static RateCard? CardFor(IReadOnlyList<RateCard> cards, Unit unit) =>
        cards.FirstOrDefault(c => c.UnitType == unit.Configuration)
        ?? cards.FirstOrDefault(c => c.UnitType == null);

    /// <summary>
    /// The card the board reports as active in its header.
    ///
    /// Where a project prices by configuration there is no single active card,
    /// so the most recent one stands in — the header is a "priced as of" note
    /// rather than the number any particular tile carries.
    /// </summary>
    private static RateCard? HeadlineCard(IReadOnlyList<RateCard> cards) =>
        cards.FirstOrDefault(c => c.UnitType == null) ?? cards.FirstOrDefault();

    private static RateCardDto ToRateCard(RateCard c, bool isActive) => new(
        c.Id, c.ProjectId, c.TowerId, c.UnitType, c.Label, c.EffectiveFrom, c.RatePerSqft, isActive);

    [HttpGet("projects/{projectId:int}/rate-cards")]
    public async Task<ActionResult<IReadOnlyList<RateCardDto>>> RateCards(
        int projectId, CancellationToken ct)
    {
        var cards = await Db.RateCards.AsNoTracking()
            .Where(c => c.ProjectId == projectId)
            .OrderBy(c => c.EffectiveFrom)
            .ToListAsync(ct);

        var active = HeadlineCard(await ActiveRateCardsAsync(projectId, null, ct));

        return Ok(cards.Select(c => ToRateCard(c, c.Id == active?.Id)).ToList());
    }

    /* ------------------------------------------------------------------ *
     * Status moves
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Moves a unit between Available, Held, Blocked, Booked, Sold and
    /// NotForSale.
    ///
    /// Blocking and booking need a manager's agreement. When the requester is
    /// not one, the unit is placed on hold and the request goes to the approval
    /// queue — the hold matters, because a unit that is being approved for one
    /// buyer must not stay open for another in the meantime.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("units/{id:int}/status")]
    public async Task<ActionResult<UnitStatusResultDto>> SetStatus(
        int id,
        UnitStatusRequest request,
        CancellationToken ct)
    {
        var unit = await Db.Units
            .Include(u => u.Project).Include(u => u.Tower)
            .FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw ApiException.NotFound("Unit");

        var target = Require(request.Status, UnitStatuses.All, "unit status");
        var previous = EffectiveStatus(unit);

        if (target == previous && target != UnitStatuses.Held)
        {
            return Ok(new UnitStatusResultDto(
                await ToUnitDtoAsync(unit, ct), false, null,
                $"{unit.UnitNumber} is already {Humanise(target)}."));
        }

        // Somebody else's live hold blocks every move except a manager's.
        if (unit.HeldByUserId is int holder
            && holder != Db.Tenant.UserId
            && unit.HeldUntil > DateTime.UtcNow
            && !ApprovalService.CanDecide(Db.Tenant.Role))
        {
            throw ApiException.Conflict(
                $"{unit.UnitNumber} is on hold by someone else until {unit.HeldUntil:dd MMM HH:mm}.");
        }

        var needsApproval =
            target is UnitStatuses.Blocked or UnitStatuses.Booked && !approvals.IsSelfApproving;

        var branchId = await DefaultBranchIdAsync(ct);

        if (needsApproval)
        {
            await approvals.CancelOpenAsync(
                ApprovalEntities.Unit, unit.Id, "Superseded by a newer request.", ct);

            // Park it so nobody else takes the unit while the request waits.
            ApplyCustomer(unit, request);
            unit.Status = UnitStatuses.Held;
            unit.HeldByUserId = Db.Tenant.UserId;
            unit.HeldUntil = DateTime.UtcNow.AddHours(48);
            unit.HoldReason = $"Awaiting approval to mark {Humanise(target)}.";

            var bookingSummary = target == UnitStatuses.Blocked
                ? $"{ApprovalKinds.Label(ApprovalKinds.UnitBlock)}: {unit.UnitNumber}"
                : request.EventDate is DateOnly approvalDay
                    ? $"{ApprovalKinds.Label(ApprovalKinds.UnitBooking)}: {unit.UnitNumber} on "
                      + $"{approvalDay:dd MMM yyyy} ({request.EventSlot ?? EventSlots.Evening}) for "
                      + $"{request.CustomerName ?? "a client"}"
                    : $"{ApprovalKinds.Label(ApprovalKinds.UnitBooking)}: {unit.UnitNumber} for "
                      + $"{request.CustomerName ?? "a client"} — event date required";

            if (target == UnitStatuses.Booked && request.EventDate is null)
            {
                throw ApiException.BadRequest(
                    "Confirming a space booking needs an event date.");
            }

            var approval = approvals.Request(
                ApprovalEntities.Unit,
                unit.Id,
                unit.UnitNumber,
                target == UnitStatuses.Blocked ? ApprovalKinds.UnitBlock : ApprovalKinds.UnitBooking,
                bookingSummary,
                branchId,
                request.Reason,
                unit.TotalPrice);

            RecordHistory(unit, previous, UnitStatuses.Held,
                $"Held pending approval to mark {Humanise(target)}.", request);

            await Db.SaveChangesAsync(ct);

            return Ok(new UnitStatusResultDto(
                await ToUnitDtoAsync(unit, ct),
                false,
                ToApprovalDto(approval, unit.Project?.Name ?? "—"),
                $"{unit.UnitNumber} is held for 48 hours while a manager reviews the request."));
        }

        if ((target is UnitStatuses.Booked or UnitStatuses.Blackout or UnitStatuses.Held)
            && request.EventDate is null)
        {
            throw ApiException.BadRequest(
                "This move needs an event date — holds, bookings and blackouts are per day.");
        }

        ApplyStatus(unit, target, request);

        if (request.EventDate is DateOnly eventDay
            && (target is UnitStatuses.Held or UnitStatuses.Booked
                or UnitStatuses.Blackout or UnitStatuses.Blocked))
        {
            var holdUntil = target == UnitStatuses.Held
                ? DateTime.UtcNow.AddHours(Math.Clamp(request.HoldHours ?? 24, 1, 168))
                : (DateTime?)null;

            await spaceBookings.UpsertManualAsync(
                unit,
                eventDay,
                request.EventSlot ?? EventSlots.Evening,
                target,
                holdUntil,
                request.LeadId,
                request.ContactId,
                request.QuotationId,
                request.CustomerName,
                request.Reason,
                ct);

            // Dated bookings and blackouts live on the calendar — keep the
            // catalogue open so other dates can still be sold.
            if (target is UnitStatuses.Booked or UnitStatuses.Blackout)
            {
                unit.Status = UnitStatuses.Available;
            }
        }

        if (target == UnitStatuses.Available && request.EventDate is DateOnly releaseDay)
        {
            var groupRef = request.QuotationId is int qid
                ? SpaceBookingService.GroupRefForQuotation(qid)
                : $"M-{unit.Id}-{releaseDay:yyyyMMdd}-{request.EventSlot ?? EventSlots.Evening}";
            var rows = await Db.SpaceBookings
                .Where(b => b.GroupRef == groupRef)
                .ToListAsync(ct);
            Db.SpaceBookings.RemoveRange(rows);
        }

        RecordHistory(unit, previous, unit.Status, request.Reason, request);

        await approvals.CancelOpenAsync(
            ApprovalEntities.Unit, unit.Id, "Resolved by a direct status change.", ct);

        await Db.SaveChangesAsync(ct);

        return Ok(new UnitStatusResultDto(
            await ToUnitDtoAsync(unit, ct), true, null,
            request.EventDate is DateOnly day
                ? $"{unit.UnitNumber} {Humanise(target).ToLowerInvariant()} for {day:dd MMM yyyy}."
                : $"{unit.UnitNumber} marked {Humanise(target)}."));
    }

    /// <summary>
    /// Writes the target status and the fields that go with it.
    ///
    /// Each status owns its own bookkeeping — releasing a unit has to clear the
    /// buyer as well as the hold, or the next rep sees a free flat with somebody
    /// else's name on it.
    /// </summary>
    private void ApplyStatus(Unit unit, string target, UnitStatusRequest request)
    {
        ApplyCustomer(unit, request);

        switch (target)
        {
            case UnitStatuses.Held:
                unit.HeldByUserId = Db.Tenant.UserId;
                unit.HeldUntil = DateTime.UtcNow.AddHours(Math.Clamp(request.HoldHours ?? 24, 1, 168));
                unit.HoldReason = request.Reason;
                break;

            case UnitStatuses.Available:
                unit.HeldByUserId = null;
                unit.HeldUntil = null;
                unit.HoldReason = null;
                unit.BlockReason = null;
                unit.BookedByContactId = null;
                unit.BookedByLeadId = null;
                unit.BookedQuotationId = null;
                unit.BookedAt = null;
                unit.CustomerName = null;
                unit.CustomerPhone = null;
                break;

            case UnitStatuses.Blocked:
            case UnitStatuses.Blackout:
            case UnitStatuses.NotForSale:
                unit.BlockReason = request.Reason;
                unit.HeldByUserId = null;
                unit.HeldUntil = null;
                unit.HoldReason = null;
                break;

            case UnitStatuses.Booked:
            case UnitStatuses.Sold:
                unit.BookedAt ??= DateTime.UtcNow;
                unit.HeldByUserId = null;
                unit.HeldUntil = null;
                unit.HoldReason = null;
                unit.BlockReason = null;
                break;
        }

        unit.Status = target;
    }

    private void ApplyCustomer(Unit unit, UnitStatusRequest request)
    {
        unit.BookedByLeadId = request.LeadId ?? unit.BookedByLeadId;
        unit.BookedByContactId = request.ContactId ?? unit.BookedByContactId;
        unit.CustomerName = request.CustomerName ?? unit.CustomerName;
        unit.CustomerPhone = request.CustomerPhone ?? unit.CustomerPhone;
        unit.BookedQuotationId = request.QuotationId ?? unit.BookedQuotationId;

        // Defaults to whoever is making the move: on a sales floor the person
        // booking the unit is the person who sold it.
        unit.SalesPersonId = request.SalesPersonId ?? unit.SalesPersonId ?? Db.Tenant.UserId;
    }

    private void RecordHistory(
        Unit unit, string from, string to, string? reason, UnitStatusRequest request)
    {
        Db.UnitStatusHistories.Add(new UnitStatusHistory
        {
            UnitId = unit.Id,
            FromStatus = from,
            ToStatus = to,
            Reason = reason,
            LeadId = request.LeadId,
            ContactId = request.ContactId,
            PartyName = request.CustomerName ?? unit.CustomerName,
            ActorId = Db.Tenant.UserId,
            ActorName = Db.Tenant.UserName,
        });
    }

    /// <summary>
    /// Everything the unit detail window shows, in one call — the unit, the
    /// rest of its floor, its quotations, its people and its trail.
    /// </summary>
    [HttpGet("units/{id:int}/dossier")]
    public async Task<ActionResult<UnitDossierDto>> Dossier(int id, CancellationToken ct)
    {
        var unit = await Db.Units.AsNoTracking()
            .Include(u => u.Project).Include(u => u.Tower)
            .FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw ApiException.NotFound("Unit");

        // The whole floor, because the floor plate highlights this unit among
        // its neighbours rather than showing it alone.
        var floorUnits = await Db.Units.AsNoTracking()
            .Where(u => u.ProjectId == unit.ProjectId
                && u.TowerId == unit.TowerId
                && u.Floor == unit.Floor)
            .ToListAsync(ct);

        var rateCards = await ActiveRateCardsAsync(unit.ProjectId, unit.TowerId, ct);
        var rateCard = CardFor(rateCards, unit);

        var holderIds = floorUnits.Where(u => u.HeldByUserId != null)
            .Select(u => u.HeldByUserId!.Value).Distinct().ToList();

        var holders = holderIds.Count == 0
            ? []
            : await Db.Users.Where(u => holderIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        var floorIds = floorUnits.Select(u => u.Id).ToList();
        var pending = await Db.Approvals
            .Where(a => a.EntityType == ApprovalEntities.Unit
                && a.Status == ApprovalStatuses.Pending
                && floorIds.Contains(a.EntityId))
            .ToDictionaryAsync(a => a.EntityId, a => a.Id, ct);

        var quotations = await Db.Quotations.AsNoTracking()
            .Include(q => q.Owner)
            .Where(q => q.UnitId == id)
            .OrderByDescending(q => q.IssueDate)
            .Select(q => new UnitQuotationDto(
                q.Id, q.QuoteNumber, q.Version, q.Status, q.ApprovalStatus,
                q.CustomerName, q.PaymentPlanName, q.DiscountPercent, q.Total,
                q.IssueDate, q.ValidUntil, q.SentAt, q.Owner!.Name))
            .Take(50)
            .ToListAsync(ct);

        var history = await Db.UnitStatusHistories.AsNoTracking()
            .Where(h => h.UnitId == id)
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new UnitHistoryDto(
                h.Id, h.FromStatus, h.ToStatus, h.Reason,
                h.LeadId, h.ContactId, h.PartyName, h.ActorName, h.CreatedAt))
            .Take(100)
            .ToListAsync(ct);

        var openApproval = await Db.Approvals.AsNoTracking()
            .Include(a => a.Branch)
            .FirstOrDefaultAsync(a => a.EntityType == ApprovalEntities.Unit
                && a.EntityId == id
                && a.Status == ApprovalStatuses.Pending, ct);

        return Ok(new UnitDossierDto(
            ToBoardUnit(unit, rateCard, holders, pending, booking: null, dateMode: false),
            unit.Project?.Name ?? "—",
            unit.Tower?.Name,
            floorUnits.OrderBy(PositionOf)
                .Select(u => ToBoardUnit(u, CardFor(rateCards, u), holders, pending, booking: null, dateMode: false))
                .ToList(),
            quotations,
            history,
            await CustomerAsync(unit, ct),
            await SalesPersonAsync(unit, ct),
            openApproval is null
                ? null
                : ToApprovalDto(openApproval, openApproval.Branch?.Name ?? "—")));
    }

    /// <summary>
    /// The buyer, preferring the linked contact or lead over the denormalised
    /// name — the linked record is the one with a phone number that is still
    /// current, and the one worth opening from here.
    /// </summary>
    private async Task<UnitPartyDto?> CustomerAsync(Unit unit, CancellationToken ct)
    {
        if (unit.BookedByContactId is int contactId)
        {
            var contact = await Db.Contacts.AsNoTracking()
                .Where(c => c.Id == contactId)
                .Select(c => new { c.Id, c.FullName, c.Phone, c.Email })
                .FirstOrDefaultAsync(ct);

            if (contact is not null)
            {
                return new UnitPartyDto(
                    contact.Id, contact.FullName, contact.Phone, contact.Email,
                    "Contact", null, contact.Id);
            }
        }

        if (unit.BookedByLeadId is int leadId)
        {
            var lead = await Db.Leads.AsNoTracking()
                .Where(l => l.Id == leadId)
                .Select(l => new { l.Id, l.Name, l.Phone, l.Email })
                .FirstOrDefaultAsync(ct);

            if (lead is not null)
            {
                return new UnitPartyDto(
                    lead.Id, lead.Name, lead.Phone, lead.Email, "Lead", lead.Id, null);
            }
        }

        return unit.CustomerName is null
            ? null
            : new UnitPartyDto(null, unit.CustomerName, unit.CustomerPhone, null, null, null, null);
    }

    private async Task<UnitPartyDto?> SalesPersonAsync(Unit unit, CancellationToken ct)
    {
        var id = unit.SalesPersonId ?? unit.HeldByUserId;
        if (id is null) return null;

        var user = await Db.Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new { u.Id, u.Name, u.Email, u.Role })
            .FirstOrDefaultAsync(ct);

        return user is null
            ? (unit.SalesPersonName is null
                ? null
                : new UnitPartyDto(null, unit.SalesPersonName, null, null, null, null, null))
            : new UnitPartyDto(user.Id, user.Name, null, user.Email, user.Role, null, null);
    }

    [HttpGet("units/{id:int}/history")]
    public async Task<ActionResult<IReadOnlyList<UnitHistoryDto>>> History(int id, CancellationToken ct)
    {
        var history = await Db.UnitStatusHistories.AsNoTracking()
            .Where(h => h.UnitId == id)
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new UnitHistoryDto(
                h.Id, h.FromStatus, h.ToStatus, h.Reason,
                h.LeadId, h.ContactId, h.PartyName, h.ActorName, h.CreatedAt))
            .Take(200)
            .ToListAsync(ct);

        return Ok(history);
    }

    /* ------------------------------------------------------------------ *
     * Shared helpers
     * ------------------------------------------------------------------ */

    /// <summary>
    /// The branch an approval is raised against. A user is scoped to one or
    /// more branches; the first is the one they work out of. Company-wide roles
    /// carry no branch list, so they fall back to the company's first branch.
    /// </summary>
    private async Task<int> DefaultBranchIdAsync(CancellationToken ct) =>
        Db.Tenant.BranchIds.Count > 0
            ? Db.Tenant.BranchIds[0]
            : await Db.Branches.Where(b => b.CompanyId == Db.Tenant.CompanyId)
                .Select(b => b.Id).FirstAsync(ct);

    internal static ApprovalDto ToApprovalDto(Approval a, string branchName) => new(
        a.Id, a.EntityType, a.EntityId, a.EntityLabel, a.Kind, a.Status, a.Summary,
        a.Reason, a.Amount, a.RequestedById, a.RequestedByName, a.RequestedAt,
        a.DecidedById, a.DecidedByName, a.DecidedAt, a.DecisionNote,
        a.BranchId, branchName,
        Math.Round((DateTime.UtcNow - a.RequestedAt).TotalHours, 1));

    private async Task<UnitDto> ToUnitDtoAsync(Unit unit, CancellationToken ct)
    {
        var heldBy = unit.HeldByUserId is null
            ? null
            : await Db.Users.Where(u => u.Id == unit.HeldByUserId)
                .Select(u => u.Name).FirstOrDefaultAsync(ct);

        return new UnitDto(
            unit.Id, unit.ProjectId, unit.Project?.Name ?? "—", unit.TowerId, unit.Tower?.Name,
            unit.UnitNumber, unit.Floor, unit.Configuration,
            unit.CarpetArea, unit.BuiltUpArea, unit.SuperArea, unit.AreaUnit,
            unit.Facing, unit.ViewType, unit.Bathrooms, unit.Balconies, unit.ParkingSlots,
            unit.IsCornerUnit, unit.VastuCompliant,
            unit.SeatingCapacity, unit.FloatingCapacity, unit.TheatreCapacity,
            unit.IsAirConditioned, unit.IsOutdoor, unit.HasStage, unit.HasAttachedKitchen,
            EffectiveStatus(unit),
            unit.BasePrice, unit.PricePerSqft, unit.FloorRisePremium, unit.PlcCharges,
            unit.TotalPrice,
            unit.PricePerPlate, unit.MinimumPlates, unit.PeakDatePremium, unit.SecurityDeposit,
            unit.HeldByUserId, heldBy, unit.HeldUntil, unit.HoldReason,
            unit.BookedByContactId, unit.BookedAt, unit.CreatedAt);
    }
}
