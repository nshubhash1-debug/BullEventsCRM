using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// Kits, pull sheets and utilisation — the three things the godown needed once
/// the register itself was in.
///
/// Shares the <c>api/props</c> prefix with <see cref="PropsController"/> the way
/// <see cref="VenuePackagesController"/> shares <c>api/inventory</c>: the same
/// resource, split across files so neither grows past reading.
/// </summary>
[ApiController]
[Route("api/props")]
[Authorize]
[SecuredBy(SecuredObjects.Unit)]
public class PropKitsController(AppDbContext db) : CrmControllerBase(db)
{
    private IQueryable<PropKit> Base() => Db.PropKits
        .Include(k => k.Lines).ThenInclude(l => l.Item).ThenInclude(i => i!.Category)
        .Include(k => k.Lines).ThenInclude(l => l.Item).ThenInclude(i => i!.Photos);

    /// <summary>
    /// Reserved quantity per item across a window.
    ///
    /// A local copy of the same sum <see cref="PropsController"/> runs, kept
    /// here rather than shared through a service because it is eight lines and
    /// the two controllers ask slightly different questions of the result.
    /// </summary>
    private async Task<Dictionary<int, int>> HeldAsync(
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<int> itemIds,
        int? excludeIssueId,
        CancellationToken ct)
    {
        if (itemIds.Count == 0) return [];

        var now = DateTime.UtcNow;

        var query = Db.PropReservations.AsNoTracking()
            .Where(r => r.Status == PropIssueStatuses.Reserved)
            .Where(r => r.FromDate <= to && r.ToDate >= from)
            .Where(r => r.ExpiresAt == null || r.ExpiresAt > now)
            .Where(r => itemIds.Contains(r.PropItemId));

        if (excludeIssueId is int exclude)
            query = query.Where(r => r.PropIssueId != exclude);

        return await query
            .GroupBy(r => r.PropItemId)
            .Select(g => new { ItemId = g.Key, Held = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.ItemId, x => x.Held, ct);
    }

    /* ------------------------------------------------------------------ *
     * Kits
     * ------------------------------------------------------------------ */

    /// <summary>
    /// The kit list. Pass a window and every line reports what the godown can
    /// actually supply for it, so a kit that cannot be fielded says so here
    /// rather than at the loading bay.
    /// </summary>
    [HttpGet("kits")]
    public async Task<ActionResult<IReadOnlyList<PropKitDto>>> Kits(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] bool activeOnly = true,
        CancellationToken ct = default)
    {
        var query = Base().AsNoTracking().AsQueryable();
        if (activeOnly) query = query.Where(k => k.IsActive);

        var kits = await query.OrderBy(k => k.Name).Take(200).ToListAsync(ct);

        Dictionary<int, int> held = [];
        if (from is DateOnly f && to is DateOnly t)
        {
            if (t < f) throw ApiException.BadRequest("The end date cannot fall before the start date.");

            var ids = kits.SelectMany(k => k.Lines).Select(l => l.PropItemId).Distinct().ToList();
            held = await HeldAsync(f, t, ids, null, ct);
        }

        var dated = from is not null && to is not null;
        return Ok(kits.Select(k => ToDto(k, dated ? held : null)).ToList());
    }

    [HttpGet("kits/{id:int}")]
    public async Task<ActionResult<PropKitDto>> GetKit(
        int id,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct = default)
    {
        var kit = await Base().AsNoTracking().FirstOrDefaultAsync(k => k.Id == id, ct)
            ?? throw ApiException.NotFound("Kit");

        Dictionary<int, int>? held = null;
        if (from is DateOnly f && to is DateOnly t)
        {
            held = await HeldAsync(f, t, kit.Lines.Select(l => l.PropItemId).ToList(), null, ct);
        }

        return Ok(ToDto(kit, held));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("kits")]
    public async Task<ActionResult<PropKitDto>> CreateKit(PropKitInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw ApiException.BadRequest("A kit name is required.");

        var code = string.IsNullOrWhiteSpace(input.Code)
            ? await NextCodeAsync(ct)
            : input.Code.Trim().ToUpperInvariant();

        if (await Db.PropKits.AnyAsync(k => k.Code == code, ct))
            throw ApiException.Conflict($"Kit code {code} is already in use.");

        var kit = new PropKit
        {
            CompanyId = Db.Tenant.CompanyId,
            Name = input.Name.Trim(),
            Code = code,
            Description = input.Description,
            SetupType = input.SetupType,
            EventType = input.EventType,
            CoverImageUrl = input.CoverImageUrl,
            RentalRatePerDay = input.RentalRatePerDay,
            SetupHours = input.SetupHours,
            CrewRequired = input.CrewRequired,
            IsActive = input.IsActive,
        };

        if (kit.EventType is not null)
            Require(kit.EventType, EventTypes.All, "event type");

        Db.PropKits.Add(kit);
        await Db.SaveChangesAsync(ct);

        return Ok(await GetKitDtoAsync(kit.Id, ct));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPut("kits/{id:int}")]
    public async Task<ActionResult<PropKitDto>> UpdateKit(
        int id, PropKitInput input, CancellationToken ct)
    {
        var kit = await Db.PropKits.FirstOrDefaultAsync(k => k.Id == id, ct)
            ?? throw ApiException.NotFound("Kit");

        kit.Name = input.Name.Trim();
        kit.Description = input.Description;
        kit.SetupType = input.SetupType;
        kit.EventType = input.EventType;
        kit.CoverImageUrl = input.CoverImageUrl;
        kit.RentalRatePerDay = input.RentalRatePerDay;
        kit.SetupHours = input.SetupHours;
        kit.CrewRequired = input.CrewRequired;
        kit.IsActive = input.IsActive;

        if (kit.EventType is not null)
            Require(kit.EventType, EventTypes.All, "event type");

        await Db.SaveChangesAsync(ct);
        return Ok(await GetKitDtoAsync(kit.Id, ct));
    }

    [PermissionAction(ObjectAction.Delete)]
    [HttpDelete("kits/{id:int}")]
    public async Task<IActionResult> DeleteKit(int id, CancellationToken ct)
    {
        var kit = await Db.PropKits.FirstOrDefaultAsync(k => k.Id == id, ct)
            ?? throw ApiException.NotFound("Kit");

        SoftDelete(kit);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("kits/{id:int}/lines")]
    public async Task<ActionResult<PropKitDto>> AddKitLine(
        int id, PropKitLineInput input, CancellationToken ct)
    {
        var kit = await Db.PropKits.Include(k => k.Lines)
            .FirstOrDefaultAsync(k => k.Id == id, ct)
            ?? throw ApiException.NotFound("Kit");

        if (input.Quantity <= 0)
            throw ApiException.BadRequest("Quantity must be at least one.");

        var item = await Db.PropItems.FirstOrDefaultAsync(i => i.Id == input.PropItemId, ct)
            ?? throw ApiException.BadRequest("Select a valid item.");

        var existing = kit.Lines.FirstOrDefault(l => l.PropItemId == item.Id);

        if (existing is not null)
        {
            existing.Quantity = input.Quantity;
            existing.IsOptional = input.IsOptional;
            existing.Notes = input.Notes;
        }
        else
        {
            kit.Lines.Add(new PropKitLine
            {
                PropKitId = kit.Id,
                PropItemId = item.Id,
                Quantity = input.Quantity,
                IsOptional = input.IsOptional,
                Notes = input.Notes,
                SortOrder = kit.Lines.Count == 0 ? 0 : kit.Lines.Max(l => l.SortOrder) + 1,
            });
        }

        await Db.SaveChangesAsync(ct);
        return Ok(await GetKitDtoAsync(kit.Id, ct));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpDelete("kits/{id:int}/lines/{lineId:int}")]
    public async Task<ActionResult<PropKitDto>> RemoveKitLine(
        int id, int lineId, CancellationToken ct)
    {
        var kit = await Db.PropKits.Include(k => k.Lines)
            .FirstOrDefaultAsync(k => k.Id == id, ct)
            ?? throw ApiException.NotFound("Kit");

        var line = kit.Lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw ApiException.NotFound("Line");

        kit.Lines.Remove(line);
        Db.PropKitLines.Remove(line);

        await Db.SaveChangesAsync(ct);
        return Ok(await GetKitDtoAsync(kit.Id, ct));
    }

    /// <summary>
    /// Drops a whole kit onto a gate pass.
    ///
    /// Every line is checked against the pass's own window before anything is
    /// written, and the whole action fails if an essential line cannot be
    /// supplied — a mandap missing its pillars is not a mandap, and half-adding
    /// one would leave a coordinator believing they had a set they do not have.
    /// Optional lines that fall short are quietly reduced rather than refused.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("issues/{issueId:int}/apply-kit")]
    public async Task<ActionResult<PropIssueDto>> ApplyKit(
        int issueId, ApplyKitInput input, CancellationToken ct)
    {
        var issue = await Db.PropIssues.Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == issueId, ct)
            ?? throw ApiException.NotFound("Gate pass");

        if (issue.Status is not PropIssueStatuses.Draft)
            throw ApiException.BadRequest("A kit can only be added while the pass is a draft.");

        var kit = await Base().FirstOrDefaultAsync(k => k.Id == input.PropKitId, ct)
            ?? throw ApiException.BadRequest("Select a valid kit.");

        var multiplier = Math.Max(1, input.Multiplier);

        var wanted = kit.Lines
            .Where(l => !input.EssentialOnly || !l.IsOptional)
            .OrderBy(l => l.SortOrder)
            .ToList();

        if (wanted.Count == 0)
            throw ApiException.BadRequest("That kit has no lines to add.");

        var itemIds = wanted.Select(l => l.PropItemId).ToList();
        var held = await HeldAsync(
            issue.DispatchDate, issue.ExpectedReturnDate, itemIds, issue.Id, ct);

        var items = await Db.PropItems
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, ct);

        var shortfalls = new List<string>();
        var toAdd = new List<(PropItem Item, int Quantity)>();

        foreach (var line in wanted)
        {
            if (!items.TryGetValue(line.PropItemId, out var item)) continue;
            if (item.Status != PropItemStatuses.Active) continue;

            var already = issue.Lines.FirstOrDefault(l => l.PropItemId == item.Id);
            var free = item.GoodQuantity
                - held.GetValueOrDefault(item.Id)
                - (already?.ReservedQuantity ?? 0);

            var need = line.Quantity * multiplier;

            if (need > free)
            {
                if (!line.IsOptional)
                {
                    shortfalls.Add(
                        $"{item.Name}: need {need}, {Math.Max(0, free)} free");
                    continue;
                }

                // Optional and short — take what there is rather than refusing.
                need = Math.Max(0, free);
                if (need == 0) continue;
            }

            toAdd.Add((item, need));
        }

        if (shortfalls.Count > 0)
        {
            throw ApiException.BadRequest(
                $"{kit.Name} cannot be fielded between {issue.DispatchDate:dd MMM} and " +
                $"{issue.ExpectedReturnDate:dd MMM} — " + string.Join("; ", shortfalls) + ".");
        }

        var order = issue.Lines.Count == 0 ? 0 : issue.Lines.Max(l => l.SortOrder) + 1;

        foreach (var (item, quantity) in toAdd)
        {
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
                    RatePerDay = item.RentalRatePerDay,
                    ChargeableDays = Math.Max(
                        1, issue.ExpectedReturnDate.DayNumber - issue.DispatchDate.DayNumber + 1),
                    Notes = $"From kit {kit.Code}",
                    SortOrder = order++,
                });
            }
        }

        if (string.IsNullOrWhiteSpace(issue.Notes))
        {
            issue.Notes = $"Includes {kit.Name}"
                + (multiplier > 1 ? $" ×{multiplier}" : "");
        }

        await Db.SaveChangesAsync(ct);

        var saved = await Db.PropIssues.AsNoTracking()
            .Include(i => i.Store)
            .Include(i => i.SiteInCharge)
            .Include(i => i.Lines).ThenInclude(l => l.Item).ThenInclude(x => x!.Category)
            .Include(i => i.Lines).ThenInclude(l => l.Item).ThenInclude(x => x!.Photos)
            .FirstAsync(i => i.Id == issue.Id, ct);

        return Ok(PropsController.Project(saved, null));
    }

    /* ------------------------------------------------------------------ *
     * Pull sheet
     * ------------------------------------------------------------------ */

    /// <summary>
    /// What the loaders carry, grouped by where it sits in the godown.
    ///
    /// Grouped by <see cref="PropItem.StorageLocation"/> rather than by category
    /// on purpose: a picker walks the shed shelf by shelf, and a list ordered by
    /// "brass items" sends them back and forth across it.
    /// </summary>
    [HttpGet("issues/{issueId:int}/pull-sheet")]
    public async Task<ActionResult<PullSheetDto>> PullSheet(int issueId, CancellationToken ct)
    {
        var issue = await Db.PropIssues.AsNoTracking()
            .Include(i => i.SiteInCharge)
            .Include(i => i.Lines).ThenInclude(l => l.Item)
            .FirstOrDefaultAsync(i => i.Id == issueId, ct)
            ?? throw ApiException.NotFound("Gate pass");

        var lines = issue.Lines
            .Where(l => l.Item is not null)
            .Select(l =>
            {
                var item = l.Item!;
                var quantity = l.IssuedQuantity > 0 ? l.IssuedQuantity : l.ReservedQuantity;

                // A packing unit of five means five pieces to a crate, so
                // seventeen pieces is four crates — the fourth part-full.
                var crates = item.PackingUnit is int pack && pack > 0
                    ? (int)Math.Ceiling(quantity / (double)pack)
                    : quantity;

                return new PullSheetLineDto(
                    item.Id, item.Name, item.Code, item.Unit, item.Size,
                    item.StorageLocation, quantity, item.PackingUnit, crates,
                    item.WeightKg,
                    item.WeightKg is decimal w ? w * quantity : null,
                    item.IsFragile, l.Notes);
            })
            .Where(l => l.Quantity > 0)
            .ToList();

        var groups = lines
            .GroupBy(l => string.IsNullOrWhiteSpace(l.StorageLocation)
                ? "Unshelved"
                : l.StorageLocation!)
            .OrderBy(g => g.Key)
            .Select(g => new PullSheetGroupDto(
                g.Key,
                g.Sum(l => l.Quantity),
                g.Any(l => l.LineWeightKg is not null) ? g.Sum(l => l.LineWeightKg ?? 0) : null,
                g.OrderBy(l => l.ItemName).ToList()))
            .ToList();

        return Ok(new PullSheetDto(
            issue.Id, issue.Code, issue.Status,
            issue.EventName, issue.ClientName, issue.VenueName, issue.VenueAddress,
            issue.DispatchDate, issue.EventDate, issue.ExpectedReturnDate,
            issue.VehicleNumber, issue.DriverName, issue.SiteInCharge?.Name,
            lines.Count,
            lines.Sum(l => l.Quantity),
            lines.Sum(l => l.Crates),
            lines.Any(l => l.LineWeightKg is not null) ? lines.Sum(l => l.LineWeightKg ?? 0) : null,
            lines.Count(l => l.IsFragile),
            groups));
    }

    /* ------------------------------------------------------------------ *
     * Utilisation
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Which pieces earn their shelf space and which have not moved.
    ///
    /// Read entirely off the stock ledger, which already holds every dispatch,
    /// return and loss. <c>DaysOut</c> counts the days a piece was actually
    /// away, taken from the gate passes' own windows, so a prop out for three
    /// four-day weddings reads as twelve days rather than as three dispatches.
    /// </summary>
    [HttpGet("utilisation")]
    public async Task<ActionResult<PropUtilisationDto>> Utilisation(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int? categoryId,
        [FromQuery] int take = 25,
        CancellationToken ct = default)
    {
        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? end.AddDays(-365);

        if (end < start)
            throw ApiException.BadRequest("The end date cannot fall before the start date.");

        var windowDays = end.DayNumber - start.DayNumber + 1;
        var limit = Math.Clamp(take, 1, 100);

        var itemsQuery = Db.PropItems.AsNoTracking()
            .Include(i => i.Category)
            .Include(i => i.Photos)
            .Where(i => i.Status != PropItemStatuses.Retired);

        if (categoryId is int cid) itemsQuery = itemsQuery.Where(i => i.CategoryId == cid);

        var items = await itemsQuery.ToListAsync(ct);
        var itemIds = items.Select(i => i.Id).ToHashSet();

        // Every issue line whose pass overlapped the window, with the pass's
        // dates alongside so the days-out sum has something to measure.
        var activity = await Db.PropIssueLines.AsNoTracking()
            .Include(l => l.Issue)
            .Where(l => l.Issue != null
                && l.Issue.Status != PropIssueStatuses.Draft
                && l.Issue.Status != PropIssueStatuses.Cancelled
                && l.Issue.DispatchDate <= end
                && l.Issue.ExpectedReturnDate >= start)
            .Select(l => new
            {
                l.PropItemId,
                l.IssuedQuantity,
                l.ReservedQuantity,
                l.DamagedQuantity,
                l.LostQuantity,
                l.RatePerDay,
                l.ChargeableDays,
                From = l.Issue!.DispatchDate,
                To = l.Issue.ActualReturnDate ?? l.Issue.ExpectedReturnDate,
            })
            .ToListAsync(ct);

        var byItem = activity
            .Where(a => itemIds.Contains(a.PropItemId))
            .GroupBy(a => a.PropItemId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = items.Select(item =>
        {
            var runs = byItem.GetValueOrDefault(item.Id) ?? [];

            var issued = runs.Sum(r => r.IssuedQuantity > 0 ? r.IssuedQuantity : r.ReservedQuantity);
            var damaged = runs.Sum(r => r.DamagedQuantity);
            var lost = runs.Sum(r => r.LostQuantity);

            // Distinct days, so two passes overlapping the same Saturday count
            // that Saturday once — the item was out, not out twice.
            var days = new HashSet<int>();
            foreach (var run in runs)
            {
                var runStart = run.From < start ? start : run.From;
                var runEnd = run.To > end ? end : run.To;

                for (var d = runStart.DayNumber; d <= runEnd.DayNumber; d++) days.Add(d);
            }

            var revenue = runs.Sum(r =>
                (r.RatePerDay ?? item.RentalRatePerDay ?? 0)
                * (r.IssuedQuantity > 0 ? r.IssuedQuantity : r.ReservedQuantity)
                * Math.Max(1, r.ChargeableDays));

            var lossValue = (item.ReplacementValue ?? 0) * (damaged + lost);

            return new PropUtilisationRowDto(
                item.Id, item.Name, item.Code, item.Category?.Name ?? "", item.Unit,
                item.Photos.OrderBy(p => p.SortOrder).FirstOrDefault() is { } photo
                    ? photo.ThumbnailUrl ?? photo.Url
                    : null,
                item.GoodQuantity,
                runs.Count, issued, damaged, lost,
                days.Count,
                windowDays == 0 ? 0 : Math.Round(days.Count / (decimal)windowDays, 3),
                revenue, lossValue,
                runs.Count == 0 ? null : runs.Max(r => r.From));
        }).ToList();

        return Ok(new PropUtilisationDto(
            start, end, windowDays,
            rows.Count,
            rows.Count(r => r.TimesIssued == 0),
            rows.Sum(r => r.EstimatedRevenue),
            rows.Sum(r => r.LossValue),
            rows.Where(r => r.TimesIssued > 0)
                .OrderByDescending(r => r.UtilisationRate).ThenByDescending(r => r.PiecesIssued)
                .Take(limit).ToList(),
            rows.Where(r => r.TimesIssued == 0)
                // Idle stock ranked by what it cost to own — the most expensive
                // thing gathering dust is the one worth a decision.
                .OrderByDescending(r => (r.GoodQuantity) * 1m)
                .ThenBy(r => r.ItemName)
                .Take(limit).ToList()));
    }

    /* ------------------------------------------------------------------ *
     * Projection
     * ------------------------------------------------------------------ */

    private async Task<PropKitDto> GetKitDtoAsync(int id, CancellationToken ct)
    {
        var kit = await Base().AsNoTracking().FirstOrDefaultAsync(k => k.Id == id, ct)
            ?? throw ApiException.NotFound("Kit");

        return ToDto(kit, null);
    }

    private async Task<string> NextCodeAsync(CancellationToken ct)
    {
        var count = await Db.PropKits.IgnoreQueryFilters()
            .CountAsync(k => k.CompanyId == Db.Tenant.CompanyId, ct);

        for (var attempt = 0; attempt < 50; attempt++)
        {
            var code = $"KIT-{count + 1 + attempt:D3}";
            if (!await Db.PropKits.AnyAsync(k => k.Code == code, ct)) return code;
        }

        return Code("KIT");
    }

    private static PropKitDto ToDto(PropKit kit, Dictionary<int, int>? held)
    {
        var lines = kit.Lines.OrderBy(l => l.SortOrder).Select(l =>
        {
            var item = l.Item;
            int? free = null;
            bool? isShort = null;

            if (held is not null && item is not null)
            {
                free = Math.Max(0, item.GoodQuantity - held.GetValueOrDefault(item.Id));
                isShort = free < l.Quantity;
            }

            return new PropKitLineDto(
                l.Id, l.PropItemId, item?.Name ?? "", item?.Code ?? "",
                item?.Category?.Name ?? "", item?.Unit ?? "PCS",
                item?.Photos.OrderBy(p => p.SortOrder).FirstOrDefault() is { } photo
                    ? photo.ThumbnailUrl ?? photo.Url
                    : null,
                l.Quantity, l.IsOptional, l.Notes, l.SortOrder,
                item?.GoodQuantity ?? 0, free, isShort);
        }).ToList();

        // Essential lines decide whether the kit can go; a short optional line
        // is a slightly plainer mandap, not a cancelled one.
        var shortEssential = held is null
            ? (int?)null
            : lines.Count(l => l.IsShort == true && !kit.Lines.First(k => k.Id == l.Id).IsOptional);

        return new PropKitDto(
            kit.Id, kit.Name, kit.Code, kit.Description, kit.SetupType, kit.EventType,
            kit.CoverImageUrl, kit.RentalRatePerDay,
            kit.Lines.Sum(l => (l.Item?.RentalRatePerDay ?? 0) * l.Quantity),
            kit.SetupHours, kit.CrewRequired, kit.IsActive,
            lines.Count,
            lines.Sum(l => l.Quantity),
            shortEssential,
            shortEssential is null ? null : shortEssential == 0,
            lines, kit.CreatedAt);
    }
}
