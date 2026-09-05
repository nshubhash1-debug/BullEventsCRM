using BullEvents.Api.Data;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>
/// Keeps the quotation, the unit it prices and the lead it was raised for in
/// step with one another.
///
/// These three records move together in the real world and used to move
/// separately here: a rep could quote a flat, send it, and have the inventory
/// board still show the unit as freely available while the lead sat in
/// "Contacted". The rules are gathered in one place rather than repeated at
/// each transition, because the failure mode of getting them slightly different
/// per endpoint is two reps selling the same flat.
///
/// Every method mutates tracked entities and leaves the commit to the caller,
/// so the sync can never persist for a transition that was itself rolled back.
/// </summary>
public class QuotationLifecycle(AppDbContext db, SpaceBookingService spaceBookings)
{
    /// <summary>
    /// How long a quoted space is parked for on the event calendar.
    ///
    /// Long enough that a customer can think it over, short enough that a quote
    /// nobody chased does not sit on stock for a month. Expired holds read as
    /// released everywhere, so nothing has to sweep them.
    /// </summary>
    private const int DraftHoldDays = 3;

    /* ------------------------------------------------------------------ *
     * Unit
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Parks the unit behind a live quotation.
    ///
    /// Only ever takes a unit that is genuinely free, and never touches one
    /// another user is holding — the caller has already refused those cases, and
    /// silently stealing a hold here would make that refusal meaningless.
    /// </summary>
    public async Task HoldForQuotationAsync(
        Quotation quotation, int holdDays, CancellationToken ct = default)
    {
        if (quotation.UnitId is not int unitId) return;

        var unit = await db.Units.FirstOrDefaultAsync(u => u.Id == unitId, ct);
        if (unit is null) return;

        var heldByAnother = unit.HeldByUserId is int holder
            && holder != db.Tenant.UserId
            && unit.HeldUntil > DateTime.UtcNow;

        if (heldByAnother) return;
        if (unit.Status is not (UnitStatuses.Available or UnitStatuses.Held)) return;

        var from = unit.Status;

        unit.Status = UnitStatuses.Held;
        unit.HeldByUserId = db.Tenant.UserId;
        unit.HeldUntil = DateTime.UtcNow.AddDays(holdDays);
        unit.HoldReason = $"Quoted to {quotation.CustomerName} on {quotation.QuoteNumber}";

        if (from != unit.Status)
        {
            AddHistory(unit, from, "Held behind a live quotation.", quotation);
        }

        // Date-level hold is what actually blocks the banquet calendar. The unit
        // hold above is a soft park for boards that still read catalogue status.
        if (quotation.EventDate is not null)
        {
            await spaceBookings.UpsertForQuotationAsync(
                quotation,
                UnitStatuses.Held,
                unit.HeldUntil,
                ct);
        }
    }

    /// <summary>
    /// Hands the unit back when the quotation that parked it is no longer live.
    ///
    /// Checks the hold reason before releasing: another quotation, or a manual
    /// block, may have taken the unit since, and dropping someone else's hold
    /// because this document died is exactly the bug the reason string exists to
    /// prevent.
    /// </summary>
    public async Task ReleaseIfHeldForAsync(
        Quotation quotation, string reason, CancellationToken ct = default)
    {
        if (quotation.UnitId is not int unitId) return;

        var unit = await db.Units.FirstOrDefaultAsync(u => u.Id == unitId, ct);
        if (unit is null || unit.Status != UnitStatuses.Held) return;

        var ours = unit.HoldReason?.Contains(quotation.QuoteNumber, StringComparison.OrdinalIgnoreCase)
            ?? false;
        if (!ours) return;

        var from = unit.Status;

        unit.Status = UnitStatuses.Available;
        unit.HeldByUserId = null;
        unit.HeldUntil = null;
        unit.HoldReason = null;

        AddHistory(unit, from, reason, quotation);
    }

    /// <summary>
    /// Commits the event dates once the offer is accepted.
    ///
    /// For dated event quotations the space stays in the catalogue as Available
    /// — a hall books again next week — and <see cref="SpaceBooking"/> rows
    /// carry the confirmed dates. Permanent Booked/Sold on the unit is only
    /// kept for legacy quotations with no event date.
    /// </summary>
    public async Task BookForQuotationAsync(Quotation quotation, CancellationToken ct = default)
    {
        if (quotation.UnitId is not int unitId) return;

        var unit = await db.Units.FirstOrDefaultAsync(u => u.Id == unitId, ct);
        if (unit is null) return;

        if (unit.Status is UnitStatuses.Sold or UnitStatuses.NotForSale) return;

        var from = unit.Status;
        var salesperson = quotation.OwnerId is int ownerId
            ? await db.Users.Where(u => u.Id == ownerId).Select(u => u.Name).FirstOrDefaultAsync(ct)
            : null;

        if (quotation.EventDate is not null)
        {
            unit.Status = UnitStatuses.Available;
            unit.HeldByUserId = null;
            unit.HeldUntil = null;
            unit.HoldReason = null;
            unit.BookedAt = null;
            unit.BookedByLeadId = quotation.LeadId;
            unit.BookedByContactId = quotation.ContactId;
            unit.BookedQuotationId = quotation.Id;
            unit.CustomerName = quotation.CustomerName;
            unit.CustomerPhone = quotation.CustomerPhone;
            unit.SalesPersonId = quotation.OwnerId;
            unit.SalesPersonName = salesperson;

            AddHistory(unit, from, $"Dates confirmed against {quotation.QuoteNumber}.", quotation);

            await spaceBookings.UpsertForQuotationAsync(
                quotation,
                UnitStatuses.Booked,
                holdExpiresAt: null,
                ct);
            return;
        }

        unit.Status = UnitStatuses.Booked;
        unit.HeldByUserId = null;
        unit.HeldUntil = null;
        unit.HoldReason = null;

        unit.BookedAt = DateTime.UtcNow;
        unit.BookedByLeadId = quotation.LeadId;
        unit.BookedByContactId = quotation.ContactId;
        unit.BookedQuotationId = quotation.Id;
        unit.CustomerName = quotation.CustomerName;
        unit.CustomerPhone = quotation.CustomerPhone;
        unit.SalesPersonId = quotation.OwnerId;
        unit.SalesPersonName = salesperson;

        AddHistory(unit, from, $"Booked against {quotation.QuoteNumber}.", quotation);
    }

    private void AddHistory(Unit unit, string from, string reason, Quotation quotation)
    {
        db.UnitStatusHistories.Add(new UnitStatusHistory
        {
            UnitId = unit.Id,
            FromStatus = from,
            ToStatus = unit.Status,
            Reason = reason,
            LeadId = quotation.LeadId,
            ContactId = quotation.ContactId,
            PartyName = quotation.CustomerName,
            ActorId = db.Tenant.UserId,
            ActorName = db.Tenant.UserName,
        });
    }

    /* ------------------------------------------------------------------ *
     * Lead
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Writes the quotation onto the lead's timeline, and advances the stage
    /// when the quotation is evidence the lead has moved.
    ///
    /// Only ever moves a lead forward, and never past a stage it has already
    /// reached: issuing a revised quote to a lead already marked Booked must not
    /// drag it back to Negotiation.
    /// </summary>
    public async Task LogOnLeadAsync(
        Quotation quotation,
        string remark,
        string? advanceTo = null,
        CancellationToken ct = default)
    {
        if (quotation.LeadId is not int leadId) return;

        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == leadId, ct);
        if (lead is null) return;

        db.LeadActivities.Add(new LeadActivity
        {
            LeadId = lead.Id,
            Type = LeadActivityTypes.Note,
            Remarks = remark,
            ActorId = db.Tenant.UserId,
            ActorName = db.Tenant.UserName,
        });

        if (advanceTo is null) return;
        if (!ShouldAdvance(lead.Stage, advanceTo)) return;

        var from = lead.Stage;
        lead.Stage = advanceTo;

        db.LeadActivities.Add(new LeadActivity
        {
            LeadId = lead.Id,
            Type = LeadActivityTypes.StageChange,
            FromStage = from,
            ToStage = advanceTo,
            Remarks = remark,
            ActorId = db.Tenant.UserId,
            ActorName = db.Tenant.UserName,
        });
    }

    /// <summary>
    /// Stage order for the forward-only rule. Closed and Lost are terminal and
    /// are never moved by a document — a lost lead receiving a quotation is a
    /// conversation for a human, not an automatic reopen.
    /// </summary>
    private static readonly string[] Ladder =
    [
        LeadStages.New,
        LeadStages.Contacted,
        LeadStages.Qualified,
        LeadStages.SiteVisit,
        LeadStages.ProposalSent,
        LeadStages.ContractSent,
        LeadStages.Booked,
    ];

    private static bool ShouldAdvance(string current, string target)
    {
        var from = Array.IndexOf(Ladder, current);
        var to = Array.IndexOf(Ladder, target);
        return from >= 0 && to >= 0 && to > from;
    }

    /* ------------------------------------------------------------------ *
     * Opportunity
     * ------------------------------------------------------------------ */

    /// <summary>Moves the deal when the paperwork behind it moves.</summary>
    public async Task AdvanceOpportunityAsync(
        Quotation quotation, string stage, int probability, CancellationToken ct = default)
    {
        if (quotation.OpportunityId is not int opportunityId) return;

        var opportunity = await db.Opportunities
            .FirstOrDefaultAsync(o => o.Id == opportunityId, ct);

        if (opportunity is null) return;
        if (!OpportunityStages.Open.Contains(opportunity.Stage)) return;

        opportunity.Stage = stage;
        opportunity.StageEnteredAt = DateTime.UtcNow;
        opportunity.Probability = Math.Max(opportunity.Probability, probability);
        opportunity.ForecastCategory = ForecastCategories.Commit;
    }

    /* ------------------------------------------------------------------ *
     * Composed transitions
     * ------------------------------------------------------------------ */

    /// <summary>A draft was raised: park the stock, note it on the lead.</summary>
    public async Task OnDraftedAsync(Quotation quotation, CancellationToken ct = default)
    {
        await HoldForQuotationAsync(quotation, DraftHoldDays, ct);
        await LogOnLeadAsync(
            quotation,
            $"Quotation {quotation.QuoteNumber} drafted for "
                + $"{quotation.UnitNumber ?? "a unit"} at {quotation.GrandTotal:N0}.",
            ct: ct);
    }

    /// <summary>
    /// The offer left the building: hold the unit for as long as the quotation
    /// is valid, and move the lead into Negotiation.
    /// </summary>
    public async Task OnIssuedAsync(Quotation quotation, CancellationToken ct = default)
    {
        var days = Math.Max(1, (int)Math.Ceiling((quotation.ValidUntil - DateTime.UtcNow).TotalDays));

        await HoldForQuotationAsync(quotation, days, ct);
        await LogOnLeadAsync(
            quotation,
            $"Quotation {quotation.QuoteNumber} issued for "
                + $"{quotation.UnitNumber ?? "a unit"} at {quotation.GrandTotal:N0}, "
                + $"valid to {quotation.ValidUntil:dd MMM yyyy}.",
            LeadStages.ProposalSent,
            ct);
        await AdvanceOpportunityAsync(quotation, OpportunityStages.Negotiation, 60, ct);
    }

    /// <summary>The buyer said yes: book the unit and move everything behind it.</summary>
    public async Task OnAcceptedAsync(Quotation quotation, CancellationToken ct = default)
    {
        await BookForQuotationAsync(quotation, ct);
        await LogOnLeadAsync(
            quotation,
            $"Quotation {quotation.QuoteNumber} accepted. "
                + $"{quotation.UnitNumber ?? "Unit"} booked at {quotation.GrandTotal:N0}.",
            LeadStages.Booked,
            ct);
        await AdvanceOpportunityAsync(quotation, OpportunityStages.Negotiation, 85, ct);
    }

    /// <summary>The document is dead: hand the stock back.</summary>
    public async Task OnClosedWithoutSaleAsync(
        Quotation quotation, string reason, CancellationToken ct = default)
    {
        await ReleaseIfHeldForAsync(quotation, reason, ct);
        await spaceBookings.ReleaseForQuotationAsync(quotation.Id, ct);
        await ReleasePropHoldsAsync(quotation, ct);
        await LogOnLeadAsync(quotation, $"{quotation.QuoteNumber}: {reason}", ct: ct);
    }

    /// <summary>
    /// Drops the soft holds a live proposal was keeping on godown stock.
    ///
    /// The holds would lapse on their own — they carry the quotation's validity
    /// as an expiry, which is the whole point of an expiring reservation — but a
    /// proposal that is explicitly dead should free the crates today rather than
    /// in a fortnight. Converted lines are left alone: their stock belongs to a
    /// gate pass now, not to the proposal.
    /// </summary>
    private async Task ReleasePropHoldsAsync(Quotation quotation, CancellationToken ct)
    {
        var holding = await db.QuotationResources
            .Where(r => r.QuotationId == quotation.Id)
            .Where(r => r.State == QuotationResourceStates.Held)
            .Where(r => r.PropReservationId != null)
            .ToListAsync(ct);

        if (holding.Count == 0) return;

        var reservationIds = holding.Select(r => r.PropReservationId!.Value).ToList();

        var reservations = await db.PropReservations
            .Where(r => reservationIds.Contains(r.Id))
            .ToListAsync(ct);

        db.PropReservations.RemoveRange(reservations);

        foreach (var resource in holding)
        {
            resource.PropReservationId = null;
            resource.State = QuotationResourceStates.Released;
        }
    }
}
