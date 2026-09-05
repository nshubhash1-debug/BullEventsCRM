using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>
/// Turns a won quotation into a booking, and drives the money after it.
///
/// The one rule everything here is arranged around: a unit is sold once. Two
/// reps closing the same flat within a minute of each other is not a
/// hypothetical in this business, and the only defence that actually holds is
/// the database refusing the second write — not a status check the application
/// performed a moment earlier.
/// </summary>
public class BookingService(
    AppDbContext db,
    BookingLedger ledger,
    TenantContext tenant,
    WebhookDispatcher webhooks,
    SpaceBookingService spaceBookings)
{
    /* ------------------------------------------------------------------ *
     * Booking
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Books a unit against a quotation.
    ///
    /// Everything in here happens inside one transaction: the unit changes
    /// status, the booking is written, its schedule is copied, and the document
    /// checklist is generated. Half of that landing is worse than none of it —
    /// a booked unit with no schedule bills nobody, and a schedule with no unit
    /// is a customer with no flat.
    /// </summary>
    public async Task<Booking> BookAsync(
        int quotationId,
        DateTime bookingDate,
        int? ownerId,
        IReadOnlyList<BookingApplicant> applicants,
        CancellationToken ct = default)
    {
        // The context retries on transient failures, and a retrying strategy
        // refuses a transaction it did not open — because retrying half of one
        // is worse than not retrying at all. Handing it the whole unit lets it
        // replay booking, schedule, checklist and unit status together, which is
        // the only replay that leaves the database consistent.
        var strategy = db.Database.CreateExecutionStrategy();

        var booking = await strategy.ExecuteAsync(() =>
            BookCoreAsync(quotationId, bookingDate, ownerId, applicants, ct));

        await ledger.RecomputeAsync(booking.Id, ct: ct);

        await webhooks.RaiseAsync("booking.created", new
        {
            id = booking.Id,
            bookingNumber = booking.BookingNumber,
            project = booking.ProjectName,
            unit = booking.UnitNumber,
            grandTotal = booking.GrandTotal,
            customer = applicants.FirstOrDefault()?.Name,
        }, ct);

        return booking;
    }

    /// <summary>
    /// The transactional half, run through the execution strategy.
    ///
    /// Everything that has to land together lives here; the ledger recompute and
    /// the webhook sit outside, because neither should be replayed if the
    /// transaction is.
    /// </summary>
    private async Task<Booking> BookCoreAsync(
        int quotationId,
        DateTime bookingDate,
        int? ownerId,
        IReadOnlyList<BookingApplicant> applicants,
        CancellationToken ct)
    {
        var quotation = await db.Quotations
            .Include(q => q.Milestones)
            .Include(q => q.Charges)
            .Include(q => q.Project)
            .FirstOrDefaultAsync(q => q.Id == quotationId, ct)
            ?? throw ApiException.NotFound("Quotation");

        if (quotation.UnitId is not int unitId)
        {
            throw ApiException.BadRequest(
                "This quotation is not against a specific unit, so there is nothing to allot.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var unit = await db.Units
            .Include(u => u.Tower)
            .FirstOrDefaultAsync(u => u.Id == unitId, ct)
            ?? throw ApiException.NotFound("Unit");

        // Read the status inside the transaction; the row is locked for the rest
        // of it.
        //
        // Booked is allowed through deliberately. Accepting a quotation on the
        // sales desk already takes the flat off the board, so by the time
        // anybody opens the booking the unit is normally Booked — refusing that
        // state made the two halves of the same sale contradict each other, and
        // the only way to open a booking was to never accept the quotation.
        // Sold, Blocked and NotForSale still stop here.
        if (unit.Status is not (UnitStatuses.Available or UnitStatuses.Held or UnitStatuses.Booked))
        {
            throw ApiException.Conflict(
                $"{unit.UnitNumber} is {unit.Status.ToLowerInvariant()} and cannot be booked.");
        }

        // Event quotations share a space across many dates — conflict is an
        // overlapping event window, not "this hall has ever been booked".
        if (quotation.EventDate is DateTime eventStart)
        {
            var eventEnd = quotation.EventEndDate ?? eventStart;
            var slot = string.IsNullOrWhiteSpace(quotation.EventSlot)
                ? EventSlots.Evening
                : quotation.EventSlot;

            var dateClash = await db.Bookings
                .Where(b => b.UnitId == unitId
                    && b.Status != BookingStatuses.Cancelled
                    && b.EventDate != null)
                .ToListAsync(ct);

            var overlaps = dateClash.Any(b =>
            {
                var otherStart = b.EventDate!.Value;
                var otherEnd = b.EventEndDate ?? otherStart;
                var windowsOverlap = otherStart <= eventEnd && otherEnd >= eventStart;
                return windowsOverlap
                    && EventSlots.Overlaps(b.EventSlot ?? EventSlots.FullDay, slot);
            });

            if (overlaps)
            {
                throw ApiException.Conflict(
                    $"{unit.UnitNumber} already has a live booking that overlaps "
                    + $"{eventStart:dd MMM yyyy} ({slot}).");
            }
        }
        else
        {
            var already = await db.Bookings
                .AnyAsync(b => b.UnitId == unitId && b.Status != BookingStatuses.Cancelled, ct);

            if (already)
            {
                throw ApiException.Conflict($"{unit.UnitNumber} already has a live booking.");
            }
        }

        // What letting Booked through gives up: the status alone no longer
        // proves the reservation belongs to this deal. So ask directly, rather
        // than allotting a flat that a different accepted quotation is holding.
        var heldElsewhere = await db.Quotations
            .AnyAsync(q => q.UnitId == unitId
                && q.Id != quotationId
                && q.Status == QuotationStatuses.Accepted, ct);

        if (heldElsewhere)
        {
            throw ApiException.Conflict(
                $"{unit.UnitNumber} is held by another accepted quotation.");
        }

        /* ---------------- split the money the way the paperwork needs it ---------------- */

        // The agreement value is the registrable consideration: the unit cost
        // alone, which the quotation already keeps apart as its Subtotal — rate
        // times area with the preferential-location premium priced into the
        // rate. Every charge row is outside the deed, and folding them in would
        // overpay stamp duty on every single sale.
        var agreementValue = quotation.Subtotal;
        var otherCharges = quotation.Charges.Sum(c => c.BasicAmount);

        var grandTotal = quotation.GrandTotal == 0 ? quotation.Total : quotation.GrandTotal;

        var booking = new Booking
        {
            CompanyId = tenant.CompanyId,
            BranchId = quotation.BranchId,
            BookingNumber = await NextNumberAsync("BKG", ct),
            QuotationId = quotation.Id,
            LeadId = quotation.LeadId,
            ContactId = quotation.ContactId,

            UnitId = unit.Id,
            ProjectId = quotation.ProjectId,
            ProjectName = quotation.Project?.Name ?? "Project",
            TowerName = unit.Tower?.Name ?? quotation.TowerName,
            UnitNumber = unit.UnitNumber,
            Configuration = unit.Configuration,
            SaleableArea = quotation.SaleableArea,
            AreaUnit = unit.AreaUnit,

            // Carried from the accepted proposal, not from the lead: the
            // contract is for the date and the head count that were offered and
            // agreed, whatever the enquiry has drifted to since.
            EventType = quotation.EventType,
            EventDate = quotation.EventDate,
            EventEndDate = quotation.EventEndDate,
            EventSlot = quotation.EventSlot,
            Functions = quotation.Functions,
            GuestCount = quotation.GuestCount,
            MinimumPlates = quotation.MinimumPlates,

            AgreementValue = agreementValue,
            OtherCharges = otherCharges,
            TaxAmount = quotation.TaxAmount + quotation.Charges.Sum(c => c.TaxAmount),
            GrandTotal = grandTotal,

            PaymentPlanId = quotation.PaymentPlanId,
            PaymentPlanName = quotation.PaymentPlanName,

            BookingDate = bookingDate,
            OwnerId = ownerId ?? quotation.OwnerId,
            Status = BookingStatuses.Booked,
        };

        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct);

        /* ---------------- the people ---------------- */

        var order = 0;
        foreach (var applicant in applicants)
        {
            applicant.CompanyId = tenant.CompanyId;
            applicant.BookingId = booking.Id;
            applicant.SortOrder = order++;
            db.BookingApplicants.Add(applicant);
        }

        /* ---------------- the schedule ---------------- */

        foreach (var milestone in quotation.Milestones.OrderBy(m => m.SortOrder))
        {
            db.BookingMilestones.Add(new BookingMilestone
            {
                CompanyId = tenant.CompanyId,
                BookingId = booking.Id,
                SortOrder = milestone.SortOrder,
                Label = milestone.Label,
                Percent = milestone.Percent,
                BasicAmount = milestone.BasicAmount,
                TaxAmount = milestone.TaxAmount,
                TotalAmount = milestone.TotalAmount,
                DueDate = milestone.DueDate,
                ConstructionStage = ConstructionStages.Detect(milestone.Label),
                Status = MilestoneStatuses.Pending,
            });
        }

        // The plan's percentages are computed on the unit consideration, so a
        // schedule copied straight from the quotation bills the flat and not the
        // parking, the club or the deposits. Left alone, that money is owed and
        // never demanded — the customer is simply never invoiced for it.
        //
        // The difference becomes its own instalment rather than being spread
        // across the others, so the schedule the customer signed still reads the
        // way they signed it and the extra line says plainly what it is.
        var scheduled = quotation.Milestones.Sum(m => m.TotalAmount);
        var unscheduled = Math.Round(grandTotal - scheduled, 2);

        if (unscheduled > 1m)
        {
            var last = quotation.Milestones
                .OrderByDescending(m => m.SortOrder)
                .FirstOrDefault();

            db.BookingMilestones.Add(new BookingMilestone
            {
                CompanyId = tenant.CompanyId,
                BookingId = booking.Id,
                SortOrder = (last?.SortOrder ?? 0) + 1,
                Label = "Other charges and deposits",
                Percent = 0,
                BasicAmount = unscheduled,
                TaxAmount = 0,
                TotalAmount = unscheduled,

                // Collected with the last construction instalment, which is when
                // these are collected in practice.
                ConstructionStage = last is null ? null : ConstructionStages.Detect(last.Label),
                DueDate = last?.DueDate,
                Status = MilestoneStatuses.Pending,
            });
        }

        /* ---------------- the file ---------------- */

        db.BookingAgreements.Add(new BookingAgreement
        {
            CompanyId = tenant.CompanyId,
            BookingId = booking.Id,
            ConsiderationValue = agreementValue,
            Status = AgreementStatuses.NotStarted,
        });

        db.Possessions.Add(new Possession
        {
            CompanyId = tenant.CompanyId,
            BookingId = booking.Id,
            Status = PossessionStatuses.NotDue,
        });

        foreach (var document in BuildChecklist(booking.Id, applicants.Count, hasLoan: false))
        {
            db.BookingDocuments.Add(document);
        }

        /* ---------------- the space ---------------- */

        var wasStatus = unit.Status;
        unit.HeldByUserId = null;
        unit.HeldUntil = null;
        unit.HoldReason = null;

        if (quotation.EventDate is not null)
        {
            // Hall stays bookable for other dates; calendar rows carry the hold.
            unit.Status = UnitStatuses.Available;
            unit.BookedQuotationId = quotation.Id;
            unit.CustomerName = quotation.CustomerName;
            unit.CustomerPhone = quotation.CustomerPhone;

            db.UnitStatusHistories.Add(new UnitStatusHistory
            {
                UnitId = unit.Id,
                FromStatus = wasStatus,
                ToStatus = UnitStatuses.Available,
                Reason = $"Event booking {booking.BookingNumber} — dates on calendar.",
                LeadId = booking.LeadId,
                ContactId = booking.ContactId,
                PartyName = applicants.FirstOrDefault()?.Name,
                ActorId = tenant.UserId,
                ActorName = tenant.UserName,
            });

            await spaceBookings.UpsertForQuotationAsync(
                quotation, UnitStatuses.Booked, holdExpiresAt: null, ct);

            var groupRef = SpaceBookingService.GroupRefForQuotation(quotation.Id);
            var rows = await db.SpaceBookings
                .Where(b => b.GroupRef == groupRef)
                .ToListAsync(ct);
            foreach (var row in rows) row.BookingId = booking.Id;
        }
        else
        {
            unit.Status = UnitStatuses.Booked;

            db.UnitStatusHistories.Add(new UnitStatusHistory
            {
                UnitId = unit.Id,
                FromStatus = wasStatus,
                ToStatus = UnitStatuses.Booked,
                Reason = $"Booked as {booking.BookingNumber}.",
                LeadId = booking.LeadId,
                ContactId = booking.ContactId,
                PartyName = applicants.FirstOrDefault()?.Name,
                ActorId = tenant.UserId,
                ActorName = tenant.UserName,
            });
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return booking;
    }

    /* ------------------------------------------------------------------ *
     * Demands
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Raises a demand for one instalment.
    ///
    /// The due date is offered rather than assumed: a demand letter posted today
    /// with a due date of today is not a demand, it is a grievance, and the
    /// penal interest clock has to start from something the customer had a
    /// chance to meet.
    /// </summary>
    public async Task<Demand> RaiseAsync(
        int bookingId,
        int milestoneId,
        DateTime? dueDate,
        decimal? interestRate,
        CancellationToken ct = default)
    {
        var milestone = await db.BookingMilestones
            .FirstOrDefaultAsync(m => m.Id == milestoneId && m.BookingId == bookingId, ct)
            ?? throw ApiException.NotFound("Instalment");

        if (milestone.Status is MilestoneStatuses.Paid or MilestoneStatuses.Waived)
        {
            throw ApiException.Conflict(
                $"{milestone.Label} is already {milestone.Status.ToLowerInvariant()}.");
        }

        var existing = await db.Demands.AnyAsync(
            d => d.BookingMilestoneId == milestoneId && d.Status != DemandStatuses.Cancelled, ct);

        if (existing)
        {
            throw ApiException.Conflict($"{milestone.Label} has already been demanded.");
        }

        var demand = new Demand
        {
            CompanyId = tenant.CompanyId,
            BookingId = bookingId,
            BookingMilestoneId = milestoneId,
            DemandNumber = await NextNumberAsync("DEM", ct),
            Label = milestone.Label,
            RaisedOn = DateTime.UtcNow.Date,

            // Fifteen days is the convention here, and it is what the agreement
            // usually words. Overridable, because some plans say seven.
            DueDate = (dueDate ?? DateTime.UtcNow.Date.AddDays(15)).Date,

            BasicAmount = milestone.BasicAmount,
            TaxAmount = milestone.TaxAmount,
            TotalAmount = milestone.TotalAmount,
            InterestRatePercent = interestRate ?? 12m,
            Status = DemandStatuses.Raised,
        };

        db.Demands.Add(demand);
        await db.SaveChangesAsync(ct);

        milestone.Status = MilestoneStatuses.Demanded;
        milestone.DemandId = demand.Id;
        await db.SaveChangesAsync(ct);

        await ledger.RecomputeAsync(bookingId, ct: ct);

        await webhooks.RaiseAsync("demand.raised", new
        {
            id = demand.Id,
            demandNumber = demand.DemandNumber,
            bookingId,
            label = demand.Label,
            amount = demand.TotalAmount,
            dueDate = demand.DueDate,
        }, ct);

        return demand;
    }

    /// <summary>
    /// Raises every instalment tied to a construction stage, across a whole
    /// tower or project, in one action.
    ///
    /// This is the operation post-sales actually runs. A slab is cast on Tower B
    /// and forty demands become due the same morning; doing that one booking at
    /// a time is an afternoon of clicking and a guarantee that two get missed.
    /// Bookings that are cancelled, already demanded for that stage, or fully
    /// paid are skipped rather than failing the run — a bulk action that stops
    /// on the first oddity is a bulk action nobody trusts.
    /// </summary>
    public async Task<BulkDemandResult> RaiseByStageAsync(
        string constructionStage,
        int? projectId,
        string? towerName,
        DateTime? dueDate,
        decimal? interestRate,
        CancellationToken ct = default)
    {
        var query = db.Bookings
            .Where(b => BookingStatuses.Live.Contains(b.Status));

        if (projectId is int project) query = query.Where(b => b.ProjectId == project);
        if (!string.IsNullOrWhiteSpace(towerName)) query = query.Where(b => b.TowerName == towerName);

        var bookings = await query.Select(b => b.Id).ToListAsync(ct);

        var candidates = await db.BookingMilestones
            .Where(m => bookings.Contains(m.BookingId))
            .Where(m => m.ConstructionStage == constructionStage)
            .Where(m => m.Status == MilestoneStatuses.Pending)
            .OrderBy(m => m.BookingId)
            .ToListAsync(ct);

        var raised = new List<Demand>();
        var skipped = new List<string>();

        foreach (var milestone in candidates)
        {
            try
            {
                raised.Add(await RaiseAsync(
                    milestone.BookingId, milestone.Id, dueDate, interestRate, ct));
            }
            catch (ApiException error)
            {
                skipped.Add($"Booking {milestone.BookingId}: {error.Message}");
            }
        }

        return new BulkDemandResult(
            raised.Count,
            raised.Sum(d => d.TotalAmount),
            skipped.Count,
            skipped.Take(20).ToList());
    }

    /* ------------------------------------------------------------------ *
     * Receipts
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Records money in and applies it.
    ///
    /// A cleared receipt is allocated immediately; a pending one is not, because
    /// an uncleared cheque that has already paid down a demand makes the ageing
    /// report lie until it bounces.
    /// </summary>
    public async Task<Receipt> ReceiveAsync(Receipt receipt, CancellationToken ct = default)
    {
        receipt.CompanyId = tenant.CompanyId;
        receipt.ReceiptNumber = await NextNumberAsync("RCP", ct);

        if (receipt.Status == ReceiptStatuses.Cleared && receipt.ClearedOn is null)
        {
            receipt.ClearedOn = receipt.ReceivedOn;
        }

        db.Receipts.Add(receipt);
        await db.SaveChangesAsync(ct);

        await RecordEscrowAsync(receipt, ct);

        if (receipt.Status == ReceiptStatuses.Cleared)
        {
            await ledger.AllocateAsync(receipt.Id, ct);
        }
        else
        {
            await ledger.RecomputeAsync(receipt.BookingId, ct: ct);
        }

        await webhooks.RaiseAsync("payment.received", new
        {
            id = receipt.Id,
            receiptNumber = receipt.ReceiptNumber,
            bookingId = receipt.BookingId,
            amount = receipt.Amount,
            mode = receipt.Mode,
            status = receipt.Status,
        }, ct);

        return receipt;
    }

    /// <summary>
    /// Marks a cheque returned.
    ///
    /// The allocations go with it, so the demands it had settled become due
    /// again — and the interest the ledger recomputes for them covers the whole
    /// period, because the money was never actually there.
    /// </summary>
    public async Task BounceAsync(int receiptId, string reason, CancellationToken ct = default)
    {
        var receipt = await db.Receipts.FirstOrDefaultAsync(r => r.Id == receiptId, ct)
            ?? throw ApiException.NotFound("Receipt");

        if (receipt.Status == ReceiptStatuses.Bounced) return;

        receipt.Status = ReceiptStatuses.Bounced;
        receipt.BouncedOn = DateTime.UtcNow;
        receipt.BounceReason = reason;
        receipt.Unallocated = 0;

        var allocations = await db.ReceiptAllocations
            .Where(a => a.ReceiptId == receiptId)
            .ToListAsync(ct);

        db.ReceiptAllocations.RemoveRange(allocations);

        var escrow = await db.EscrowEntries.Where(e => e.ReceiptId == receiptId).ToListAsync(ct);
        db.EscrowEntries.RemoveRange(escrow);

        await db.SaveChangesAsync(ct);
        await ledger.RecomputeAsync(receipt.BookingId, ct: ct);
    }

    /// <summary>Clears a pending instrument and applies it.</summary>
    public async Task ClearAsync(int receiptId, DateTime clearedOn, CancellationToken ct = default)
    {
        var receipt = await db.Receipts.FirstOrDefaultAsync(r => r.Id == receiptId, ct)
            ?? throw ApiException.NotFound("Receipt");

        receipt.Status = ReceiptStatuses.Cleared;
        receipt.ClearedOn = clearedOn;
        receipt.BouncedOn = null;
        receipt.BounceReason = null;

        await db.SaveChangesAsync(ct);
        await RecordEscrowAsync(receipt, ct);
        await ledger.AllocateAsync(receipt.Id, ct);
    }

    /// <summary>
    /// Splits a receipt for the seventy-per-cent rule.
    ///
    /// Recorded as the money arrives rather than reconstructed at audit, which
    /// is the whole point — a year of bank statements does not tell anybody
    /// which deposit belonged to which project.
    /// </summary>
    private async Task RecordEscrowAsync(Receipt receipt, CancellationToken ct)
    {
        if (receipt.Status != ReceiptStatuses.Cleared) return;

        var already = await db.EscrowEntries.AnyAsync(e => e.ReceiptId == receipt.Id, ct);
        if (already) return;

        var projectId = await db.Bookings
            .Where(b => b.Id == receipt.BookingId)
            .Select(b => b.ProjectId)
            .FirstOrDefaultAsync(ct);

        var designated = Math.Round(receipt.CreditedAmount * 0.70m, 2);

        db.EscrowEntries.Add(new EscrowEntry
        {
            CompanyId = receipt.CompanyId,
            ProjectId = projectId,
            BookingId = receipt.BookingId,
            ReceiptId = receipt.Id,
            On = receipt.ReceivedOn,
            ReceiptAmount = receipt.CreditedAmount,
            DesignatedPercent = 70m,
            DesignatedAmount = designated,
            FreeAmount = receipt.CreditedAmount - designated,
        });

        await db.SaveChangesAsync(ct);
    }

    /* ------------------------------------------------------------------ *
     * helpers
     * ------------------------------------------------------------------ */

    /// <summary>
    /// The next number in a per-company, per-year series.
    ///
    /// Counted from the rows that exist rather than from a counter table: a
    /// counter drifts the first time a transaction rolls back, and a booking
    /// number that skips is a question somebody has to answer at audit.
    /// </summary>
    private async Task<string> NextNumberAsync(string prefix, CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var stem = $"BRG/{prefix}/{year}/";

        // Collated to "C" for the comparison. These columns carry the CRM's
        // case-folding ICU collation so that searching for a customer folds
        // case, and Postgres refuses LIKE against a non-deterministic collation
        // outright — which is what a plain StartsWith compiles to.
        //
        // Filters off as well: a cancelled or soft-deleted booking still holds
        // its number, and counting only the visible ones would hand the next
        // booking a number the unique index already has.
        var used = prefix switch
        {
            "BKG" => await db.Bookings.IgnoreQueryFilters().CountAsync(
                b => EF.Functions.Collate(b.BookingNumber, "C").StartsWith(stem), ct),

            "DEM" => await db.Demands.IgnoreQueryFilters().CountAsync(
                d => EF.Functions.Collate(d.DemandNumber, "C").StartsWith(stem), ct),

            _ => await db.Receipts.IgnoreQueryFilters().CountAsync(
                r => EF.Functions.Collate(r.ReceiptNumber, "C").StartsWith(stem), ct),
        };

        return $"{stem}{used + 1:0000}";
    }

    /// <summary>The papers this booking is expected to produce, as rows to tick off.</summary>
    private IEnumerable<BookingDocument> BuildChecklist(int bookingId, int applicants, bool hasLoan)
    {
        foreach (var template in DocumentCatalog.All)
        {
            if (template.LoanOnly && !hasLoan) continue;

            var copies = template.PerApplicant ? Math.Max(1, applicants) : 1;

            for (var index = 0; index < copies; index++)
            {
                yield return new BookingDocument
                {
                    CompanyId = tenant.CompanyId,
                    BookingId = bookingId,
                    Key = template.Key,
                    Name = template.PerApplicant && copies > 1
                        ? $"{template.Name} — applicant {index + 1}"
                        : template.Name,
                    Stage = template.Stage,
                    IsRequired = true,
                    Status = DocumentStatuses.Pending,
                };
            }
        }
    }
}

/// <summary>What a bulk demand run did.</summary>
public record BulkDemandResult(
    int Raised,
    decimal TotalAmount,
    int Skipped,
    IReadOnlyList<string> Reasons);
