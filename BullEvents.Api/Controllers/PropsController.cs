using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// The godown: props, décor, furniture, fabric and florals — the countable
/// stock an event goes out with, as opposed to the venue it goes out to.
///
/// Three things here are worth reading before changing anything:
///
///   1. <b>Availability is a window, not a flag.</b> Every "can we do it?"
///      question resolves through <see cref="HoldsAsync"/>, which sums the
///      reservations overlapping the requested dates. Nothing caches a
///      free/taken state on the item, because there isn't one.
///
///   2. <b>Stock is only ever moved through <see cref="ApplyMovement"/>.</b>
///      It writes the ledger row and adjusts the condition counts in the same
///      breath, so the two cannot drift. No other method touches
///      GoodQuantity directly.
///
///   3. <b>A gate pass owns its reservations.</b> Committing one writes a
///      reservation per line; cancelling or closing it releases them. The
///      reservation table is never edited on its own from outside.
/// </summary>
[ApiController]
[Route("api/props")]
[Authorize]
[SecuredBy(SecuredObjects.Unit)]
public class PropsController(AppDbContext db) : CrmControllerBase(db)
{
    private static readonly FieldMap<PropItem> Fields = new FieldMap<PropItem>()
        .Text("name", "Item", searchable: true)
        .Text("code", "Stock code", searchable: true)
        .Select("categoryName", "Category", "Category.Name")
        .Select("storeName", "Store", "Store.Name")
        .Select("itemType", "Type")
        .Select("status", "Status")
        .Select("ownership", "Ownership")
        .Text("size", "Size", searchable: true)
        .Select("colour", "Colour")
        .Select("material", "Material")
        .Select("unit", "Unit")
        .Text("tags", "Tags", searchable: true)
        .Text("storageLocation", "Storage location", searchable: true)
        .Number("goodQuantity", "Usable stock")
        .Number("repairableQuantity", "Repairable")
        .Number("damagedQuantity", "Damaged")
        .Number("reorderLevel", "Reorder level")
        .Number("rentalRatePerDay", "Rental per day")
        .Number("purchaseCost", "Purchase cost")
        .Number("replacementValue", "Replacement value")
        .Number("weightKg", "Weight (kg)")
        .Number("turnaroundDays", "Turnaround days")
        .Bool("isFragile", "Fragile")
        .Bool("isSerialised", "Serialised")
        .Number("categoryId", "Category ID")
        .Text("supplierName", "Supplier", searchable: true)
        .Date("purchaseDate", "Purchased")
        .Date("createdAt", "Created");

    private IQueryable<PropItem> Base() => Db.PropItems
        .Include(i => i.Category)
        .Include(i => i.Store)
        .Include(i => i.Owner)
        .Include(i => i.Photos)
        .AsNoTracking();

    /* ------------------------------------------------------------------ *
     * Catalogue
     * ------------------------------------------------------------------ */

    [HttpGet("fields")]
    public ActionResult<IReadOnlyList<FilterFieldDto>> FilterFields() =>
        Ok(DescribeFields(Fields, new Dictionary<string, IReadOnlyList<FilterOptionDto>>
        {
            ["itemType"] = Options(PropItemTypes.All),
            ["status"] = Options(PropItemStatuses.All),
            ["ownership"] = Options(PropOwnershipTypes.All),
        }));

    [HttpPost("items/query")]
    public async Task<ActionResult<PagedResult<PropItemDto>>> Query(
        QueryRequest request, CancellationToken ct)
    {
        var page = await RunQueryAsync(
            Base(), request, Fields, i => ToDto(i),
            defaultSortPath: nameof(PropItem.Name), defaultSortDescending: false,
            aggregates: async filtered => new Dictionary<string, decimal>
            {
                ["goodQuantity"] = await filtered.SumAsync(i => (decimal)i.GoodQuantity, ct),
                ["damagedQuantity"] = await filtered.SumAsync(i => (decimal)i.DamagedQuantity, ct),
                ["catalogueValue"] = await filtered.SumAsync(
                    i => (i.ReplacementValue ?? i.PurchaseCost ?? 0) * i.GoodQuantity, ct),
            },
            cancellationToken: ct);

        return Ok(page);
    }

    [HttpGet("items/{id:int}")]
    public async Task<ActionResult<PropItemDto>> GetItem(int id, CancellationToken ct)
    {
        var item = await Base().FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw ApiException.NotFound("Item");

        return Ok(ToDto(item));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("items")]
    public async Task<ActionResult<PropItemDto>> CreateItem(PropItemInput input, CancellationToken ct)
    {
        var category = await Db.PropCategories.FirstOrDefaultAsync(c => c.Id == input.CategoryId, ct)
            ?? throw ApiException.BadRequest("Select a valid category.");

        if (string.IsNullOrWhiteSpace(input.Name))
            throw ApiException.BadRequest("An item name is required.");

        if (input.GoodQuantity < 0 || input.RepairableQuantity < 0 || input.DamagedQuantity < 0)
            throw ApiException.BadRequest("Quantities cannot be negative.");

        var code = string.IsNullOrWhiteSpace(input.Code)
            ? await NextItemCodeAsync(category, ct)
            : input.Code.Trim().ToUpperInvariant();

        if (await Db.PropItems.AnyAsync(i => i.Code == code, ct))
            throw ApiException.Conflict($"Stock code {code} is already in use.");

        var item = new PropItem
        {
            CompanyId = Db.Tenant.CompanyId,
            CategoryId = category.Id,
            StoreId = input.StoreId,
            Name = input.Name.Trim(),
            Code = code,
            ItemType = Require(input.ItemType, PropItemTypes.All, "item type"),
            Status = Require(input.Status, PropItemStatuses.All, "status"),
            Ownership = Require(input.Ownership, PropOwnershipTypes.All, "ownership"),
            Size = input.Size?.Trim(),
            Colour = input.Colour?.Trim(),
            Material = input.Material?.Trim(),
            Unit = string.IsNullOrWhiteSpace(input.Unit) ? "PCS" : input.Unit.Trim().ToUpperInvariant(),
            Description = input.Description,
            Tags = input.Tags,
            GoodQuantity = input.GoodQuantity,
            RepairableQuantity = input.RepairableQuantity,
            DamagedQuantity = input.DamagedQuantity,
            ReorderLevel = input.ReorderLevel,
            RentalRatePerDay = input.RentalRatePerDay,
            PurchaseCost = input.PurchaseCost,
            ReplacementValue = input.ReplacementValue,
            SupplierName = input.SupplierName,
            PurchaseDate = input.PurchaseDate,
            WeightKg = input.WeightKg,
            PackingUnit = input.PackingUnit,
            IsFragile = input.IsFragile,
            IsSerialised = input.IsSerialised,
            TurnaroundDays = Math.Max(0, input.TurnaroundDays),
            StorageLocation = input.StorageLocation,
            OwnerId = input.OwnerId ?? (Db.Tenant.UserId > 0 ? Db.Tenant.UserId : null),
        };

        await RequireOwnerAsync(item.OwnerId, ct);
        Db.PropItems.Add(item);
        await Db.SaveChangesAsync(ct);

        // The opening balance is a ledger row like any other, so the stock card
        // starts from a stated figure rather than from an unexplained number.
        if (item.GoodQuantity > 0)
        {
            Db.PropStockMovements.Add(new PropStockMovement
            {
                CompanyId = item.CompanyId,
                PropItemId = item.Id,
                MovementType = PropMovementTypes.Opening,
                Quantity = item.GoodQuantity,
                ToCondition = PropConditions.Good,
                BalanceAfter = item.GoodQuantity,
                StoreId = item.StoreId,
                Notes = "Opening balance",
            });
            await Db.SaveChangesAsync(ct);
        }

        return Ok(await GetDtoAsync(item.Id, ct));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPut("items/{id:int}")]
    public async Task<ActionResult<PropItemDto>> UpdateItem(
        int id, PropItemInput input, CancellationToken ct)
    {
        var item = await Db.PropItems.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw ApiException.NotFound("Item");

        if (input.CategoryId != item.CategoryId)
        {
            var exists = await Db.PropCategories.AnyAsync(c => c.Id == input.CategoryId, ct);
            if (!exists) throw ApiException.BadRequest("Select a valid category.");
            item.CategoryId = input.CategoryId;
        }

        if (!string.IsNullOrWhiteSpace(input.Code))
        {
            var code = input.Code.Trim().ToUpperInvariant();
            if (code != item.Code && await Db.PropItems.AnyAsync(i => i.Code == code, ct))
                throw ApiException.Conflict($"Stock code {code} is already in use.");
            item.Code = code;
        }

        item.StoreId = input.StoreId;
        item.Name = input.Name.Trim();
        item.ItemType = Require(input.ItemType, PropItemTypes.All, "item type");
        item.Status = Require(input.Status, PropItemStatuses.All, "status");
        item.Ownership = Require(input.Ownership, PropOwnershipTypes.All, "ownership");
        item.Size = input.Size?.Trim();
        item.Colour = input.Colour?.Trim();
        item.Material = input.Material?.Trim();
        item.Unit = string.IsNullOrWhiteSpace(input.Unit) ? "PCS" : input.Unit.Trim().ToUpperInvariant();
        item.Description = input.Description;
        item.Tags = input.Tags;
        item.ReorderLevel = input.ReorderLevel;
        item.RentalRatePerDay = input.RentalRatePerDay;
        item.PurchaseCost = input.PurchaseCost;
        item.ReplacementValue = input.ReplacementValue;
        item.SupplierName = input.SupplierName;
        item.PurchaseDate = input.PurchaseDate;
        item.WeightKg = input.WeightKg;
        item.PackingUnit = input.PackingUnit;
        item.IsFragile = input.IsFragile;
        item.IsSerialised = input.IsSerialised;
        item.TurnaroundDays = Math.Max(0, input.TurnaroundDays);
        item.StorageLocation = input.StorageLocation;

        if (input.OwnerId != item.OwnerId)
        {
            await RequireOwnerAsync(input.OwnerId, ct);
            item.OwnerId = input.OwnerId;
        }

        // Quantities deliberately are not settable here. Editing a description
        // is a catalogue change; changing a count is a stock movement, and it
        // goes through the ledger so it leaves a trail.
        await Db.SaveChangesAsync(ct);
        return Ok(await GetDtoAsync(item.Id, ct));
    }

    [PermissionAction(ObjectAction.Delete)]
    [HttpDelete("items/{id:int}")]
    public async Task<IActionResult> DeleteItem(int id, CancellationToken ct)
    {
        var item = await Db.PropItems.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw ApiException.NotFound("Item");

        var committed = await Db.PropReservations
            .AnyAsync(r => r.PropItemId == id && r.Status == PropIssueStatuses.Reserved, ct);

        if (committed)
            throw ApiException.BadRequest(
                "This item is held for an upcoming event. Release those gate passes first.");

        SoftDelete(item);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    /* ------------------------------------------------------------------ *
     * Rates
     *
     * The register came out of a workbook that had no rate column, so all 734
     * lines landed unpriced. Until they carry a rental rate the catalogue
     * cannot be valued, a proposal cannot cost its décor and a breakage has
     * nothing to bill against — which makes this the one gap that blocks money
     * rather than merely convenience.
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Which parts of the catalogue are priced and which are not.
    ///
    /// Reported per category because that is the unit somebody actually works
    /// through: an afternoon spent pricing the brass, then the glass, then the
    /// chandeliers. A flat "612 unpriced" tells you the size of the job and
    /// nothing about where to start.
    /// </summary>
    [HttpGet("pricing-coverage")]
    public async Task<ActionResult<PropPricingSummaryDto>> PricingCoverage(CancellationToken ct)
    {
        var rows = await Db.PropCategories.AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new
            {
                c.Id,
                c.Name,
                Items = c.Items.Where(i => !i.IsDeleted).Select(i => new
                {
                    i.RentalRatePerDay,
                    i.ReplacementValue,
                    i.PurchaseCost,
                    i.GoodQuantity,
                }).ToList(),
            })
            .ToListAsync(ct);

        var byCategory = rows.Select(c =>
        {
            var priced = c.Items.Where(i => i.RentalRatePerDay > 0).ToList();

            return new PropPricingCoverageDto(
                c.Id, c.Name, c.Items.Count, priced.Count, c.Items.Count - priced.Count,
                priced.Count == 0 ? null : Math.Round(priced.Average(i => i.RentalRatePerDay!.Value), 0),
                c.Items.Sum(i => (i.ReplacementValue ?? i.PurchaseCost ?? 0) * i.GoodQuantity));
        }).ToList();

        var allItems = rows.SelectMany(c => c.Items).ToList();
        var allPriced = allItems.Where(i => i.RentalRatePerDay > 0).ToList();

        return Ok(new PropPricingSummaryDto(
            allItems.Count,
            allPriced.Count,
            allItems.Count - allPriced.Count,
            allItems.Sum(i => (i.ReplacementValue ?? i.PurchaseCost ?? 0) * i.GoodQuantity),
            allPriced.Count == 0 ? null : Math.Round(allPriced.Average(i => i.RentalRatePerDay!.Value), 0),
            byCategory));
    }

    /// <summary>
    /// Writes rates across many items in one go.
    ///
    /// Only the fields present on a row are written — a screen setting rental
    /// rates must not blank the replacement values somebody filled in last week,
    /// and a nullable-means-leave-alone contract is the only way to guarantee
    /// that without the client having to send back what it never touched.
    ///
    /// Deliberately does not go through the stock ledger: a rate is a commercial
    /// figure, not a movement, and nothing about the count on the shelf changes.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("rates")]
    public async Task<ActionResult<PropRateBulkResultDto>> SetRates(
        PropRateBulkInput input, CancellationToken ct)
    {
        if (input.Rows.Count == 0)
            throw ApiException.BadRequest("Nothing to write.");

        if (input.Rows.Count > 1000)
            throw ApiException.BadRequest("Send at most 1,000 rows at a time.");

        var ids = input.Rows.Select(r => r.PropItemId).Distinct().ToList();

        var items = await Db.PropItems
            .Where(i => ids.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, ct);

        var warnings = new List<string>();
        var updated = 0;

        foreach (var row in input.Rows)
        {
            if (!items.TryGetValue(row.PropItemId, out var item))
            {
                warnings.Add($"Item #{row.PropItemId} no longer exists — skipped.");
                continue;
            }

            if (row.RentalRatePerDay is decimal rental)
            {
                if (rental < 0)
                {
                    warnings.Add($"{item.Name}: a negative rental rate was ignored.");
                }
                else
                {
                    // Zero is a deliberate "not for hire", which is different
                    // from never having been priced — so it is stored as null
                    // rather than as a zero that would read as a free rental.
                    item.RentalRatePerDay = rental == 0 ? null : rental;
                }
            }

            if (row.ReplacementValue is decimal replacement && replacement >= 0)
            {
                item.ReplacementValue = replacement == 0 ? null : replacement;
            }

            if (row.PurchaseCost is decimal purchase && purchase >= 0)
            {
                item.PurchaseCost = purchase == 0 ? null : purchase;
            }

            if (row.ReorderLevel is int reorder && reorder >= 0)
            {
                item.ReorderLevel = reorder;
            }

            if (row.WeightKg is decimal weight && weight >= 0)
            {
                item.WeightKg = weight == 0 ? null : weight;
            }

            if (row.PackingUnit is int packing && packing >= 0)
            {
                item.PackingUnit = packing == 0 ? null : packing;
            }

            if (row.StorageLocation is not null)
            {
                var location = row.StorageLocation.Trim();
                item.StorageLocation = location.Length == 0 ? null : location;
            }

            updated++;
        }

        await Db.SaveChangesAsync(ct);

        var totals = await Db.PropItems.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Unpriced = g.Count(i => i.RentalRatePerDay == null || i.RentalRatePerDay == 0),
                Value = g.Sum(i => (i.ReplacementValue ?? i.PurchaseCost ?? 0) * i.GoodQuantity),
            })
            .FirstOrDefaultAsync(ct);

        return Ok(new PropRateBulkResultDto(
            updated, input.Rows.Count - updated,
            totals?.Unpriced ?? 0, totals?.Total ?? 0, totals?.Value ?? 0,
            warnings));
    }

    /* ---------------- photos ---------------- */

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("items/{id:int}/photos")]
    public async Task<ActionResult<PropPhotoDto>> AddPhoto(
        int id, PropPhotoInput input, CancellationToken ct)
    {
        var item = await Db.PropItems.Include(i => i.Photos)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw ApiException.NotFound("Item");

        if (string.IsNullOrWhiteSpace(input.Url))
            throw ApiException.BadRequest("A photo URL is required.");

        var photo = new PropItemPhoto
        {
            PropItemId = item.Id,
            Url = input.Url.Trim(),
            ThumbnailUrl = input.ThumbnailUrl?.Trim(),
            Caption = input.Caption,
            SortOrder = item.Photos.Count == 0 ? 0 : item.Photos.Max(p => p.SortOrder) + 1,
        };

        Db.PropItemPhotos.Add(photo);
        await Db.SaveChangesAsync(ct);

        return Ok(new PropPhotoDto(photo.Id, photo.Url, photo.ThumbnailUrl, photo.Caption, photo.SortOrder));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpDelete("photos/{photoId:int}")]
    public async Task<IActionResult> DeletePhoto(int photoId, CancellationToken ct)
    {
        var photo = await Db.PropItemPhotos
            .Include(p => p.Item)
            .FirstOrDefaultAsync(p => p.Id == photoId, ct)
            ?? throw ApiException.NotFound("Photo");

        // The join through Item is what keeps a photo id from another tenant
        // out — PropItemPhoto carries no CompanyId of its own.
        if (photo.Item is null) throw ApiException.NotFound("Photo");

        Db.PropItemPhotos.Remove(photo);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    /* ------------------------------------------------------------------ *
     * Categories and stores
     * ------------------------------------------------------------------ */

    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<PropCategoryDto>>> Categories(CancellationToken ct)
    {
        var rows = await Db.PropCategories.AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new PropCategoryDto(
                c.Id, c.Name, c.Code, c.ParentId, c.DefaultItemType, c.SortOrder, c.IsActive,
                c.Items.Count(i => !i.IsDeleted),
                c.Items.Where(i => !i.IsDeleted).Sum(i => (int?)i.GoodQuantity) ?? 0))
            .ToListAsync(ct);

        return Ok(rows);
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("categories")]
    public async Task<ActionResult<PropCategoryDto>> CreateCategory(
        PropCategoryInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Code))
            throw ApiException.BadRequest("Name and code are required.");

        var code = input.Code.Trim().ToUpperInvariant();
        if (await Db.PropCategories.AnyAsync(c => c.Code == code, ct))
            throw ApiException.Conflict($"Category code {code} already exists.");

        var category = new PropCategory
        {
            CompanyId = Db.Tenant.CompanyId,
            Name = input.Name.Trim(),
            Code = code,
            ParentId = input.ParentId,
            DefaultItemType = Require(input.DefaultItemType, PropItemTypes.All, "item type"),
            SortOrder = input.SortOrder,
            IsActive = input.IsActive,
        };

        Db.PropCategories.Add(category);
        await Db.SaveChangesAsync(ct);

        return Ok(new PropCategoryDto(
            category.Id, category.Name, category.Code, category.ParentId,
            category.DefaultItemType, category.SortOrder, category.IsActive, 0, 0));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPut("categories/{id:int}")]
    public async Task<ActionResult<PropCategoryDto>> UpdateCategory(
        int id, PropCategoryInput input, CancellationToken ct)
    {
        var category = await Db.PropCategories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw ApiException.NotFound("Category");

        if (input.ParentId == id)
            throw ApiException.BadRequest("A category cannot be its own parent.");

        category.Name = input.Name.Trim();
        category.ParentId = input.ParentId;
        category.DefaultItemType = Require(input.DefaultItemType, PropItemTypes.All, "item type");
        category.SortOrder = input.SortOrder;
        category.IsActive = input.IsActive;

        await Db.SaveChangesAsync(ct);

        var count = await Db.PropItems.CountAsync(i => i.CategoryId == id, ct);
        var good = await Db.PropItems.Where(i => i.CategoryId == id).SumAsync(i => (int?)i.GoodQuantity, ct) ?? 0;

        return Ok(new PropCategoryDto(
            category.Id, category.Name, category.Code, category.ParentId,
            category.DefaultItemType, category.SortOrder, category.IsActive, count, good));
    }

    [HttpGet("stores")]
    public async Task<ActionResult<IReadOnlyList<PropStoreDto>>> Stores(CancellationToken ct)
    {
        var rows = await Db.PropStores.AsNoTracking()
            .Include(s => s.Keeper)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        var counts = await Db.PropItems
            .Where(i => i.StoreId != null)
            .GroupBy(i => i.StoreId!.Value)
            .Select(g => new { StoreId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StoreId, x => x.Count, ct);

        return Ok(rows.Select(s => new PropStoreDto(
            s.Id, s.Name, s.Code, s.City, s.Address, s.KeeperId, s.Keeper?.Name,
            s.IsActive, counts.GetValueOrDefault(s.Id))).ToList());
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("stores")]
    public async Task<ActionResult<PropStoreDto>> CreateStore(PropStoreInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Code))
            throw ApiException.BadRequest("Name and code are required.");

        var code = input.Code.Trim().ToUpperInvariant();
        if (await Db.PropStores.AnyAsync(s => s.Code == code, ct))
            throw ApiException.Conflict($"Store code {code} already exists.");

        var store = new PropStore
        {
            CompanyId = Db.Tenant.CompanyId,
            Name = input.Name.Trim(),
            Code = code,
            City = input.City,
            Address = input.Address,
            KeeperId = input.KeeperId,
            IsActive = input.IsActive,
        };

        Db.PropStores.Add(store);
        await Db.SaveChangesAsync(ct);

        return Ok(new PropStoreDto(
            store.Id, store.Name, store.Code, store.City, store.Address,
            store.KeeperId, null, store.IsActive, 0));
    }

    /* ------------------------------------------------------------------ *
     * Availability
     *
     * The question this module exists to answer. Everything else is a way of
     * recording what happened after it was answered.
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Reserved quantity per item across a window, from the blocking holds.
    ///
    /// Returned as a dictionary rather than joined into the item query because
    /// the same numbers feed the catalogue grid, the picker and the calendar,
    /// and each of those already has its own item query.
    /// </summary>
    private async Task<Dictionary<int, List<PropReservation>>> HoldsAsync(
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<int>? itemIds,
        int? excludeIssueId,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var query = Db.PropReservations.AsNoTracking()
            .Include(r => r.Issue)
            .Where(r => r.Status == PropIssueStatuses.Reserved)
            .Where(r => r.FromDate <= to && r.ToDate >= from)
            // A lapsed soft hold stops blocking on its own. Filtered in SQL so
            // an abandoned quotation cannot quietly freeze stock for months.
            .Where(r => r.ExpiresAt == null || r.ExpiresAt > now);

        if (itemIds is { Count: > 0 })
        {
            query = query.Where(r => itemIds.Contains(r.PropItemId));
        }

        if (excludeIssueId is int exclude)
        {
            query = query.Where(r => r.PropIssueId != exclude);
        }

        var rows = await query.ToListAsync(ct);

        return rows.GroupBy(r => r.PropItemId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    [HttpPost("availability")]
    public async Task<ActionResult<IReadOnlyList<PropAvailabilityDto>>> Availability(
        PropAvailabilityRequest request, CancellationToken ct)
    {
        if (request.To < request.From)
            throw ApiException.BadRequest("The return date cannot fall before the dispatch date.");

        var items = Base().Where(i => i.Status == PropItemStatuses.Active);

        if (request.ItemIds is { Count: > 0 })
        {
            items = items.Where(i => request.ItemIds.Contains(i.Id));
        }

        if (request.CategoryId is int categoryId)
        {
            items = items.Where(i => i.CategoryId == categoryId);
        }

        var rows = await items.OrderBy(i => i.Name).Take(500).ToListAsync(ct);
        var ids = rows.Select(i => i.Id).ToList();
        var holds = await HoldsAsync(request.From, request.To, ids, request.ExcludePropIssueId, ct);

        return Ok(rows.Select(item =>
        {
            var itemHolds = holds.GetValueOrDefault(item.Id) ?? [];
            var reserved = itemHolds.Sum(h => h.Quantity);

            return new PropAvailabilityDto(
                item.Id, item.Name, item.Code, item.Category?.Name ?? "", item.Unit,
                PrimaryThumb(item),
                item.GoodQuantity,
                reserved,
                Math.Max(0, item.GoodQuantity - reserved),
                itemHolds.Select(h => new PropAvailabilityHoldDto(
                    h.Id, h.PropIssueId, h.Issue?.Code, h.Quantity, h.FromDate, h.ToDate,
                    h.EventName, h.ClientName, h.Status)).ToList());
        }).ToList());
    }

    /// <summary>
    /// A day-by-day strip per item, for the planner's calendar.
    ///
    /// Capped at 60 days and 100 items: the grid is rendered cell by cell in the
    /// browser, and past that it is a download, not a view.
    /// </summary>
    [HttpPost("calendar")]
    public async Task<ActionResult<IReadOnlyList<PropCalendarRowDto>>> Calendar(
        PropAvailabilityRequest request, CancellationToken ct)
    {
        if (request.To < request.From)
            throw ApiException.BadRequest("The end date cannot fall before the start date.");

        var days = request.To.DayNumber - request.From.DayNumber + 1;
        if (days > 60) throw ApiException.BadRequest("Ask for at most 60 days at a time.");

        var items = Base().Where(i => i.Status == PropItemStatuses.Active);

        if (request.ItemIds is { Count: > 0 })
            items = items.Where(i => request.ItemIds.Contains(i.Id));

        if (request.CategoryId is int categoryId)
            items = items.Where(i => i.CategoryId == categoryId);

        var rows = await items.OrderBy(i => i.Name).Take(100).ToListAsync(ct);
        var ids = rows.Select(i => i.Id).ToList();
        var holds = await HoldsAsync(request.From, request.To, ids, request.ExcludePropIssueId, ct);

        return Ok(rows.Select(item =>
        {
            var itemHolds = holds.GetValueOrDefault(item.Id) ?? [];
            var cells = new List<PropCalendarCellDto>(days);

            for (var d = 0; d < days; d++)
            {
                var date = request.From.AddDays(d);
                var reserved = itemHolds
                    .Where(h => h.FromDate <= date && h.ToDate >= date)
                    .Sum(h => h.Quantity);

                cells.Add(new PropCalendarCellDto(
                    date, reserved, Math.Max(0, item.GoodQuantity - reserved)));
            }

            return new PropCalendarRowDto(
                item.Id, item.Name, item.Code, item.Unit, item.GoodQuantity, cells);
        }).ToList());
    }

    /* ------------------------------------------------------------------ *
     * Stock ledger
     * ------------------------------------------------------------------ */

    [HttpGet("items/{id:int}/movements")]
    public async Task<ActionResult<IReadOnlyList<PropMovementDto>>> Movements(
        int id, [FromQuery] int take = 100, CancellationToken ct = default)
    {
        var exists = await Db.PropItems.AnyAsync(i => i.Id == id, ct);
        if (!exists) throw ApiException.NotFound("Item");

        var rows = await Db.PropStockMovements.AsNoTracking()
            .Include(m => m.Item)
            .Include(m => m.Issue)
            .Where(m => m.PropItemId == id)
            .OrderByDescending(m => m.MovedAt).ThenByDescending(m => m.Id)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(ct);

        var actorIds = rows.Select(m => m.CreatedById).OfType<int>().Distinct().ToList();
        var actors = await Db.Users.AsNoTracking()
            .Where(u => actorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        return Ok(rows.Select(m => new PropMovementDto(
            m.Id, m.PropItemId, m.Item?.Name ?? "", m.Item?.Code ?? "",
            m.MovementType, m.Quantity, m.FromCondition, m.ToCondition, m.BalanceAfter,
            m.PropIssueId, m.Issue?.Code, m.LeadId, m.Amount, m.Notes, m.HandledBy,
            m.MovedAt,
            m.CreatedById is int actor ? actors.GetValueOrDefault(actor) : null)).ToList());
    }

    /// <summary>
    /// The one place stock changes.
    ///
    /// Writes the ledger row and moves the condition counts together, so the
    /// counts are always exactly what the ledger says they should be. Does not
    /// save — the caller commits it with whatever else the operation changed,
    /// which is what keeps a half-dispatched gate pass from existing.
    /// </summary>
    /// <param name="alreadyOut">
    /// The pieces left the shelf at dispatch and are not on it now, so only the
    /// ledger row is written. This is what a loss or an on-site consumption is:
    /// the deduction happened when the truck loaded, and booking it again here
    /// would take the same pieces off the books twice.
    /// </param>
    private void ApplyMovement(
        PropItem item,
        string type,
        int quantity,
        string? fromCondition = null,
        string? toCondition = null,
        PropIssue? issue = null,
        decimal? amount = null,
        string? notes = null,
        string? handledBy = null,
        bool alreadyOut = false)
    {
        if (quantity <= 0) throw ApiException.BadRequest("Quantity must be at least one.");

        var sign = PropMovementTypes.Delta(type);
        var signed = sign * quantity;

        if (!alreadyOut)
        {
            switch (type)
            {
                // A reclassification: the pieces exist either way, they just move
                // between buckets. Both ends are adjusted so the total is untouched.
                case PropMovementTypes.Damage:
                    Take(item, fromCondition ?? PropConditions.Good, quantity);
                    Give(item, toCondition ?? PropConditions.Damaged, quantity);
                    break;

                case PropMovementTypes.Repair:
                    Take(item, fromCondition ?? PropConditions.Repairable, quantity);
                    Give(item, toCondition ?? PropConditions.Good, quantity);
                    break;

                // A stock count that adds. The subtracting case carries its own
                // sign and is written by RecordMovement directly.
                case PropMovementTypes.Adjustment:
                    Give(item, toCondition ?? PropConditions.Good, quantity);
                    break;

                default:
                    if (sign < 0) Take(item, fromCondition ?? PropConditions.Good, quantity);
                    else if (sign > 0) Give(item, toCondition ?? PropConditions.Good, quantity);
                    break;
            }
        }

        Db.PropStockMovements.Add(new PropStockMovement
        {
            CompanyId = item.CompanyId,
            PropItemId = item.Id,
            MovementType = type,
            Quantity = signed == 0 ? quantity : signed,
            FromCondition = fromCondition,
            ToCondition = toCondition,
            BalanceAfter = item.GoodQuantity,
            PropIssueId = issue?.Id,
            StoreId = issue?.StoreId ?? item.StoreId,
            LeadId = issue?.LeadId,
            BookingId = issue?.BookingId,
            Amount = amount,
            Notes = notes,
            HandledBy = handledBy,
            MovedAt = DateTime.UtcNow,
        });
    }

    /// <summary>Removes pieces from a condition bucket, refusing to go negative.</summary>
    private static void Take(PropItem item, string condition, int quantity)
    {
        var available = Read(item, condition);
        if (available < quantity)
            throw ApiException.BadRequest(
                $"{item.Name} has only {available} {condition.ToLowerInvariant()} " +
                $"{item.Unit.ToLowerInvariant()} on the books — cannot move {quantity}.");

        Write(item, condition, available - quantity);
    }

    private static void Give(PropItem item, string condition, int quantity) =>
        Write(item, condition, Read(item, condition) + quantity);

    private static int Read(PropItem item, string condition) => condition switch
    {
        PropConditions.Repairable => item.RepairableQuantity,
        PropConditions.Damaged => item.DamagedQuantity,
        _ => item.GoodQuantity,
    };

    private static void Write(PropItem item, string condition, int value)
    {
        switch (condition)
        {
            case PropConditions.Repairable: item.RepairableQuantity = value; break;
            case PropConditions.Damaged: item.DamagedQuantity = value; break;
            // Lost stock leaves the books entirely, so it lands nowhere.
            case PropConditions.Lost: break;
            default: item.GoodQuantity = value; break;
        }
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("movements")]
    public async Task<ActionResult<PropItemDto>> RecordMovement(
        PropMovementInput input, CancellationToken ct)
    {
        var item = await Db.PropItems.FirstOrDefaultAsync(i => i.Id == input.PropItemId, ct)
            ?? throw ApiException.NotFound("Item");

        var type = Require(input.MovementType, PropMovementTypes.All, "movement type");

        // Issue and return belong to a gate pass, which keeps the reservations
        // in step with the counts. Booking one by hand would move stock without
        // releasing what was held for it.
        if (type is PropMovementTypes.IssueOut or PropMovementTypes.Return)
            throw ApiException.BadRequest(
                "Dispatch and return are recorded on a gate pass, not as a manual movement.");

        if (type == PropMovementTypes.Adjustment && !input.IsIncrease)
        {
            Take(item, input.ToCondition ?? PropConditions.Good, input.Quantity);

            Db.PropStockMovements.Add(new PropStockMovement
            {
                CompanyId = item.CompanyId,
                PropItemId = item.Id,
                MovementType = PropMovementTypes.Adjustment,
                Quantity = -input.Quantity,
                ToCondition = input.ToCondition ?? PropConditions.Good,
                BalanceAfter = item.GoodQuantity,
                StoreId = item.StoreId,
                Amount = input.Amount,
                Notes = input.Notes,
                HandledBy = input.HandledBy,
                MovedAt = input.MovedAt ?? DateTime.UtcNow,
            });
        }
        else
        {
            ApplyMovement(
                item, type, input.Quantity, input.FromCondition, input.ToCondition,
                amount: input.Amount, notes: input.Notes, handledBy: input.HandledBy);
        }

        await Db.SaveChangesAsync(ct);
        return Ok(await GetDtoAsync(item.Id, ct));
    }

    /* ------------------------------------------------------------------ *
     * Gate passes
     * ------------------------------------------------------------------ */

    private IQueryable<PropIssue> IssueBase() => Db.PropIssues
        .Include(i => i.Store)
        .Include(i => i.SiteInCharge)
        .Include(i => i.Lines).ThenInclude(l => l.Item).ThenInclude(x => x!.Category)
        .Include(i => i.Lines).ThenInclude(l => l.Item).ThenInclude(x => x!.Photos);

    [HttpGet("issues")]
    public async Task<ActionResult<IReadOnlyList<PropIssueDto>>> Issues(
        [FromQuery] string? status,
        [FromQuery] int? leadId,
        [FromQuery] bool overdueOnly = false,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        var query = IssueBase().AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(i => i.Status == status);

        if (leadId is int lid)
            query = query.Where(i => i.LeadId == lid);

        if (overdueOnly)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            query = query.Where(i =>
                i.ExpectedReturnDate < today &&
                (i.Status == PropIssueStatuses.Dispatched ||
                 i.Status == PropIssueStatuses.PartiallyReturned));
        }

        var rows = await query
            .OrderByDescending(i => i.DispatchDate).ThenByDescending(i => i.Id)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(ct);

        return Ok(rows.Select(i => ToDto(i, null)).ToList());
    }

    [HttpGet("issues/{id:int}")]
    public async Task<ActionResult<PropIssueDto>> GetIssue(int id, CancellationToken ct)
    {
        var issue = await IssueBase().AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw ApiException.NotFound("Gate pass");

        // Availability on the pass's own window, with the pass itself excluded —
        // otherwise every line would read as competing with itself.
        var ids = issue.Lines.Select(l => l.PropItemId).ToList();
        var holds = await HoldsAsync(issue.DispatchDate, issue.ExpectedReturnDate, ids, issue.Id, ct);

        return Ok(ToDto(issue, holds));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("issues")]
    public async Task<ActionResult<PropIssueDto>> CreateIssue(PropIssueInput input, CancellationToken ct)
    {
        if (input.ExpectedReturnDate < input.DispatchDate)
            throw ApiException.BadRequest("The return date cannot fall before the dispatch date.");

        var issue = new PropIssue
        {
            CompanyId = Db.Tenant.CompanyId,
            Code = Code("GP"),
            Status = PropIssueStatuses.Draft,
            LeadId = input.LeadId,
            BookingId = input.BookingId,
            QuotationId = input.QuotationId,
            ProjectId = input.ProjectId,
            EventName = input.EventName,
            EventType = input.EventType,
            ClientName = input.ClientName,
            VenueName = input.VenueName,
            VenueAddress = input.VenueAddress,
            DispatchDate = input.DispatchDate,
            EventDate = input.EventDate,
            ExpectedReturnDate = input.ExpectedReturnDate,
            StoreId = input.StoreId,
            VehicleNumber = input.VehicleNumber,
            DriverName = input.DriverName,
            DriverPhone = input.DriverPhone,
            SiteInChargeId = input.SiteInChargeId,
            Notes = input.Notes,
            OwnerId = input.OwnerId ?? (Db.Tenant.UserId > 0 ? Db.Tenant.UserId : null),
        };

        // Denormalised from the lead when the caller did not spell them out, so
        // the list reads properly without a join per row.
        if (input.LeadId is int leadId)
        {
            var lead = await Db.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == leadId, ct)
                ?? throw ApiException.BadRequest("Select a valid lead.");

            issue.ClientName ??= lead.Name;
            issue.EventType ??= lead.EventType;
        }

        if (input.ProjectId is int projectId)
        {
            var project = await Db.Projects.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId, ct)
                ?? throw ApiException.BadRequest("Select a valid venue.");

            issue.VenueName ??= project.Name;
            issue.VenueAddress ??= project.Address;
        }

        await RequireOwnerAsync(issue.OwnerId, ct);
        Db.PropIssues.Add(issue);
        await Db.SaveChangesAsync(ct);

        foreach (var (line, index) in (input.Lines ?? []).Select((l, n) => (l, n)))
        {
            await AddLineAsync(issue, line, index, ct);
        }

        await Db.SaveChangesAsync(ct);
        return Ok(await GetIssueDtoAsync(issue.Id, ct));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPut("issues/{id:int}")]
    public async Task<ActionResult<PropIssueDto>> UpdateIssue(
        int id, PropIssueInput input, CancellationToken ct)
    {
        var issue = await Db.PropIssues.Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw ApiException.NotFound("Gate pass");

        if (issue.Status is PropIssueStatuses.Closed or PropIssueStatuses.Cancelled)
            throw ApiException.BadRequest($"A {issue.Status.ToLowerInvariant()} gate pass cannot be edited.");

        if (input.ExpectedReturnDate < input.DispatchDate)
            throw ApiException.BadRequest("The return date cannot fall before the dispatch date.");

        var datesMoved = issue.DispatchDate != input.DispatchDate
            || issue.ExpectedReturnDate != input.ExpectedReturnDate;

        issue.EventName = input.EventName;
        issue.EventType = input.EventType;
        issue.ClientName = input.ClientName;
        issue.VenueName = input.VenueName;
        issue.VenueAddress = input.VenueAddress;
        issue.DispatchDate = input.DispatchDate;
        issue.EventDate = input.EventDate;
        issue.ExpectedReturnDate = input.ExpectedReturnDate;
        issue.StoreId = input.StoreId;
        issue.VehicleNumber = input.VehicleNumber;
        issue.DriverName = input.DriverName;
        issue.DriverPhone = input.DriverPhone;
        issue.SiteInChargeId = input.SiteInChargeId;
        issue.Notes = input.Notes;

        // The holds are a copy of the pass's window; move one and the other has
        // to follow, or the calendar starts lying about a date nobody booked.
        if (datesMoved)
        {
            var reservations = await Db.PropReservations
                .Where(r => r.PropIssueId == issue.Id && r.Status == PropIssueStatuses.Reserved)
                .ToListAsync(ct);

            foreach (var reservation in reservations)
            {
                var item = await Db.PropItems.AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == reservation.PropItemId, ct);

                reservation.FromDate = issue.DispatchDate;
                reservation.ToDate = issue.ExpectedReturnDate.AddDays(item?.TurnaroundDays ?? 0);
            }
        }

        await Db.SaveChangesAsync(ct);
        return Ok(await GetIssueDtoAsync(issue.Id, ct));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("issues/{id:int}/lines")]
    public async Task<ActionResult<PropIssueDto>> AddLine(
        int id, PropIssueLineInput input, CancellationToken ct)
    {
        var issue = await Db.PropIssues.Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw ApiException.NotFound("Gate pass");

        if (issue.Status is PropIssueStatuses.Closed or PropIssueStatuses.Cancelled)
            throw ApiException.BadRequest("This gate pass is finished.");

        var order = issue.Lines.Count == 0 ? 0 : issue.Lines.Max(l => l.SortOrder) + 1;
        await AddLineAsync(issue, input, order, ct);
        await Db.SaveChangesAsync(ct);

        return Ok(await GetIssueDtoAsync(issue.Id, ct));
    }

    private async Task AddLineAsync(
        PropIssue issue, PropIssueLineInput input, int sortOrder, CancellationToken ct)
    {
        if (input.Quantity <= 0)
            throw ApiException.BadRequest("Quantity must be at least one.");

        var item = await Db.PropItems.FirstOrDefaultAsync(i => i.Id == input.PropItemId, ct)
            ?? throw ApiException.BadRequest($"Item #{input.PropItemId} does not exist.");

        if (item.Status != PropItemStatuses.Active)
            throw ApiException.BadRequest($"{item.Name} is {item.Status.ToLowerInvariant()} and cannot be issued.");

        var existing = issue.Lines.FirstOrDefault(l => l.PropItemId == item.Id);
        var wanted = input.Quantity + (existing?.ReservedQuantity ?? 0);

        await GuardAvailabilityAsync(issue, item, wanted, ct);

        if (existing is not null)
        {
            existing.ReservedQuantity = wanted;
            existing.RatePerDay = input.RatePerDay ?? existing.RatePerDay;
            existing.ChargeableDays = Math.Max(1, input.ChargeableDays);
            existing.Notes = input.Notes ?? existing.Notes;
        }
        else
        {
            issue.Lines.Add(new PropIssueLine
            {
                PropIssueId = issue.Id,
                PropItemId = item.Id,
                ReservedQuantity = input.Quantity,
                RatePerDay = input.RatePerDay ?? item.RentalRatePerDay,
                ChargeableDays = Math.Max(1, input.ChargeableDays),
                Notes = input.Notes,
                SortOrder = sortOrder,
            });
        }

        // A pass that already holds stock keeps its holds in step as lines
        // change; a draft holds nothing until it is committed.
        if (issue.IsBlocking)
        {
            await SyncReservationAsync(issue, item, wanted, ct);
        }
    }

    /// <summary>
    /// Refuses a line the godown cannot actually supply for these dates.
    ///
    /// Checked against the item's usable stock minus every other pass's hold
    /// over the same window — this pass excluded, so raising a line from 10 to
    /// 12 is measured against 12, not against 22.
    /// </summary>
    private async Task GuardAvailabilityAsync(
        PropIssue issue, PropItem item, int wanted, CancellationToken ct)
    {
        var window = issue.ExpectedReturnDate.AddDays(item.TurnaroundDays);
        var holds = await HoldsAsync(issue.DispatchDate, window, [item.Id], issue.Id, ct);
        var reserved = (holds.GetValueOrDefault(item.Id) ?? []).Sum(h => h.Quantity);
        var free = item.GoodQuantity - reserved;

        if (wanted > free)
        {
            throw ApiException.BadRequest(
                $"{item.Name}: only {Math.Max(0, free)} of {item.GoodQuantity} " +
                $"{item.Unit.ToLowerInvariant()} are free between " +
                $"{issue.DispatchDate:dd MMM} and {window:dd MMM} — {reserved} are held by other events.");
        }
    }

    /// <summary>Writes or updates this pass's hold on one item.</summary>
    private async Task SyncReservationAsync(
        PropIssue issue, PropItem item, int quantity, CancellationToken ct)
    {
        var reservation = await Db.PropReservations.FirstOrDefaultAsync(
            r => r.PropIssueId == issue.Id && r.PropItemId == item.Id, ct);

        var to = issue.ExpectedReturnDate.AddDays(item.TurnaroundDays);

        if (reservation is null)
        {
            Db.PropReservations.Add(new PropReservation
            {
                CompanyId = issue.CompanyId,
                PropItemId = item.Id,
                PropIssueId = issue.Id,
                LeadId = issue.LeadId,
                QuotationId = issue.QuotationId,
                BookingId = issue.BookingId,
                Quantity = quantity,
                FromDate = issue.DispatchDate,
                ToDate = to,
                Status = PropIssueStatuses.Reserved,
                EventName = issue.EventName,
                ClientName = issue.ClientName,
            });
        }
        else
        {
            reservation.Quantity = quantity;
            reservation.FromDate = issue.DispatchDate;
            reservation.ToDate = to;
            reservation.Status = PropIssueStatuses.Reserved;
        }
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpDelete("issues/{id:int}/lines/{lineId:int}")]
    public async Task<ActionResult<PropIssueDto>> RemoveLine(
        int id, int lineId, CancellationToken ct)
    {
        var issue = await Db.PropIssues.Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw ApiException.NotFound("Gate pass");

        var line = issue.Lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw ApiException.NotFound("Line");

        if (line.IssuedQuantity > 0)
            throw ApiException.BadRequest(
                "This line has already left the godown. Record a return instead of removing it.");

        var reservation = await Db.PropReservations.FirstOrDefaultAsync(
            r => r.PropIssueId == issue.Id && r.PropItemId == line.PropItemId, ct);

        if (reservation is not null) Db.PropReservations.Remove(reservation);

        issue.Lines.Remove(line);
        Db.PropIssueLines.Remove(line);
        await Db.SaveChangesAsync(ct);

        return Ok(await GetIssueDtoAsync(issue.Id, ct));
    }

    /// <summary>
    /// Commits a draft: the stock is now spoken for, and the dates block.
    ///
    /// Availability is re-checked line by line at this moment rather than
    /// trusted from when the line was added — a draft built last week may have
    /// been overtaken by a pass someone else committed since.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("issues/{id:int}/reserve")]
    public async Task<ActionResult<PropIssueDto>> Reserve(int id, CancellationToken ct)
    {
        var issue = await Db.PropIssues.Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw ApiException.NotFound("Gate pass");

        if (issue.Status != PropIssueStatuses.Draft)
            throw ApiException.BadRequest($"This gate pass is already {issue.Status.ToLowerInvariant()}.");

        if (issue.Lines.Count == 0)
            throw ApiException.BadRequest("Add at least one item before reserving.");

        foreach (var line in issue.Lines)
        {
            var item = await Db.PropItems.FirstOrDefaultAsync(i => i.Id == line.PropItemId, ct)
                ?? throw ApiException.BadRequest("An item on this pass no longer exists.");

            await GuardAvailabilityAsync(issue, item, line.ReservedQuantity, ct);
            await SyncReservationAsync(issue, item, line.ReservedQuantity, ct);
        }

        issue.Status = PropIssueStatuses.Reserved;
        await Db.SaveChangesAsync(ct);

        return Ok(await GetIssueDtoAsync(issue.Id, ct));
    }

    /// <summary>
    /// The truck leaves. Stock moves out of the godown for real.
    ///
    /// The loaded count per line is taken from the gate, not assumed from the
    /// reservation, because it routinely differs — and the difference is the
    /// whole reason the two columns are separate.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("issues/{id:int}/dispatch")]
    public async Task<ActionResult<PropIssueDto>> Dispatch(
        int id, PropDispatchInput input, CancellationToken ct)
    {
        var issue = await Db.PropIssues.Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw ApiException.NotFound("Gate pass");

        if (issue.Status is not (PropIssueStatuses.Reserved or PropIssueStatuses.Draft))
            throw ApiException.BadRequest($"A {issue.Status.ToLowerInvariant()} gate pass cannot be dispatched.");

        var byLine = input.Lines.ToDictionary(l => l.PropIssueLineId, l => l.IssuedQuantity);

        foreach (var line in issue.Lines)
        {
            var quantity = byLine.GetValueOrDefault(line.Id, line.ReservedQuantity);
            if (quantity <= 0) continue;

            var item = await Db.PropItems.FirstOrDefaultAsync(i => i.Id == line.PropItemId, ct)
                ?? throw ApiException.BadRequest("An item on this pass no longer exists.");

            ApplyMovement(
                item, PropMovementTypes.IssueOut, quantity,
                fromCondition: PropConditions.Good,
                issue: issue,
                notes: $"Dispatched on {issue.Code}" +
                       (issue.EventName is null ? "" : $" for {issue.EventName}"),
                handledBy: input.HandledBy);

            line.IssuedQuantity = quantity;

            // The hold now tracks what actually left, so a short load frees the
            // difference for another event the same weekend.
            await SyncReservationAsync(issue, item, quantity, ct);
        }

        issue.Status = PropIssueStatuses.Dispatched;
        issue.DispatchedAt = DateTime.UtcNow;
        issue.VehicleNumber = input.VehicleNumber ?? issue.VehicleNumber;
        issue.DriverName = input.DriverName ?? issue.DriverName;
        issue.DriverPhone = input.DriverPhone ?? issue.DriverPhone;

        if (!string.IsNullOrWhiteSpace(input.Notes))
        {
            issue.Notes = string.IsNullOrWhiteSpace(issue.Notes)
                ? input.Notes
                : $"{issue.Notes}\n{input.Notes}";
        }

        await LogLeadActivityAsync(
            issue.LeadId, LeadActivityTypes.Note,
            $"Props dispatched on {issue.Code} — {issue.Lines.Sum(l => l.IssuedQuantity)} pieces, " +
            $"due back {issue.ExpectedReturnDate:dd MMM yyyy}.", ct);

        await Db.SaveChangesAsync(ct);
        return Ok(await GetIssueDtoAsync(issue.Id, ct));
    }

    /// <summary>
    /// The load comes home and is counted back in.
    ///
    /// Split four ways because that is how a godown actually reconciles: good
    /// pieces go back on the shelf, damaged ones move to the damaged bucket,
    /// consumed ones are written off without complaint, and lost ones leave the
    /// books and become a line on the client's bill.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("issues/{id:int}/return")]
    public async Task<ActionResult<PropIssueDto>> Return(
        int id, PropReturnInput input, CancellationToken ct)
    {
        var issue = await Db.PropIssues.Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw ApiException.NotFound("Gate pass");

        if (issue.Status is not (PropIssueStatuses.Dispatched or PropIssueStatuses.PartiallyReturned))
            throw ApiException.BadRequest("Only a dispatched gate pass can be returned.");

        decimal recovery = 0;

        foreach (var entry in input.Lines)
        {
            var line = issue.Lines.FirstOrDefault(l => l.Id == entry.PropIssueLineId)
                ?? throw ApiException.BadRequest($"Line #{entry.PropIssueLineId} is not on this pass.");

            var item = await Db.PropItems.FirstOrDefaultAsync(i => i.Id == line.PropItemId, ct)
                ?? throw ApiException.BadRequest("An item on this pass no longer exists.");

            var total = entry.ReturnedQuantity + entry.DamagedQuantity
                + entry.LostQuantity + entry.ConsumedQuantity;

            if (total > line.PendingQuantity)
                throw ApiException.BadRequest(
                    $"{item.Name}: {total} accounted for but only {line.PendingQuantity} " +
                    $"{item.Unit.ToLowerInvariant()} are still out.");

            // Dispatch already took every one of these pieces off the shelf.
            // Coming back is what puts them on it again — so a return adds, and
            // a loss or a consumption adds nothing and merely says why the
            // pieces are not coming.

            if (entry.ReturnedQuantity > 0)
            {
                ApplyMovement(
                    item, PropMovementTypes.Return, entry.ReturnedQuantity,
                    toCondition: PropConditions.Good, issue: issue,
                    notes: $"Returned on {issue.Code}", handledBy: input.HandledBy);

                line.ReturnedQuantity += entry.ReturnedQuantity;
            }

            if (entry.DamagedQuantity > 0)
            {
                // Back on the books, but into the damaged bucket: the pieces
                // exist, they just cannot go out again.
                ApplyMovement(
                    item, PropMovementTypes.Return, entry.DamagedQuantity,
                    toCondition: PropConditions.Damaged, issue: issue,
                    notes: $"Returned damaged on {issue.Code}", handledBy: input.HandledBy);

                line.DamagedQuantity += entry.DamagedQuantity;
                recovery += (item.ReplacementValue ?? 0) * entry.DamagedQuantity;
            }

            if (entry.LostQuantity > 0)
            {
                ApplyMovement(
                    item, PropMovementTypes.Lost, entry.LostQuantity,
                    fromCondition: PropConditions.Good, issue: issue,
                    amount: (item.ReplacementValue ?? 0) * entry.LostQuantity,
                    notes: $"Not returned from {issue.Code}", handledBy: input.HandledBy,
                    alreadyOut: true);

                line.LostQuantity += entry.LostQuantity;
                recovery += (item.ReplacementValue ?? 0) * entry.LostQuantity;
            }

            if (entry.ConsumedQuantity > 0)
            {
                ApplyMovement(
                    item, PropMovementTypes.Consumed, entry.ConsumedQuantity,
                    fromCondition: PropConditions.Good, issue: issue,
                    notes: $"Consumed on {issue.Code}", handledBy: input.HandledBy,
                    alreadyOut: true);

                line.ConsumedQuantity += entry.ConsumedQuantity;
            }

            if (!string.IsNullOrWhiteSpace(entry.Notes))
            {
                line.Notes = string.IsNullOrWhiteSpace(line.Notes)
                    ? entry.Notes
                    : $"{line.Notes}\n{entry.Notes}";
            }
        }

        var pending = issue.Lines.Sum(l => l.PendingQuantity);

        issue.Status = pending == 0
            ? PropIssueStatuses.Returned
            : PropIssueStatuses.PartiallyReturned;

        if (pending == 0)
        {
            issue.ActualReturnDate = input.ReturnDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            await ReleaseReservationsAsync(issue, ct);
        }

        if (recovery > 0)
        {
            issue.DamageRecovery = (issue.DamageRecovery ?? 0) + recovery;
        }

        if (!string.IsNullOrWhiteSpace(input.Notes))
        {
            issue.Notes = string.IsNullOrWhiteSpace(issue.Notes)
                ? input.Notes
                : $"{issue.Notes}\n{input.Notes}";
        }

        await Db.SaveChangesAsync(ct);
        return Ok(await GetIssueDtoAsync(issue.Id, ct));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("issues/{id:int}/close")]
    public async Task<ActionResult<PropIssueDto>> Close(int id, CancellationToken ct)
    {
        var issue = await Db.PropIssues.Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw ApiException.NotFound("Gate pass");

        if (issue.Status is PropIssueStatuses.Closed or PropIssueStatuses.Cancelled)
            throw ApiException.BadRequest("This gate pass is already finished.");

        issue.Status = PropIssueStatuses.Closed;
        issue.ClosedAt = DateTime.UtcNow;
        issue.ActualReturnDate ??= DateOnly.FromDateTime(DateTime.UtcNow);

        await ReleaseReservationsAsync(issue, ct);
        await Db.SaveChangesAsync(ct);

        return Ok(await GetIssueDtoAsync(issue.Id, ct));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("issues/{id:int}/cancel")]
    public async Task<ActionResult<PropIssueDto>> Cancel(int id, CancellationToken ct)
    {
        var issue = await Db.PropIssues.Include(i => i.Lines)
            .FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw ApiException.NotFound("Gate pass");

        if (issue.Lines.Any(l => l.PendingQuantity > 0))
            throw ApiException.BadRequest(
                "Stock from this pass is still out. Record the return before cancelling.");

        issue.Status = PropIssueStatuses.Cancelled;
        await ReleaseReservationsAsync(issue, ct);
        await Db.SaveChangesAsync(ct);

        return Ok(await GetIssueDtoAsync(issue.Id, ct));
    }

    /// <summary>Drops this pass's holds so the dates read free again.</summary>
    private async Task ReleaseReservationsAsync(PropIssue issue, CancellationToken ct)
    {
        var reservations = await Db.PropReservations
            .Where(r => r.PropIssueId == issue.Id)
            .ToListAsync(ct);

        foreach (var reservation in reservations)
        {
            reservation.Status = PropIssueStatuses.Returned;
        }
    }

    /* ------------------------------------------------------------------ *
     * Dashboard
     * ------------------------------------------------------------------ */

    [HttpGet("dashboard")]
    public async Task<ActionResult<PropDashboardDto>> Dashboard(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekEnd = today.AddDays(7);

        var items = Db.PropItems.AsNoTracking();

        var totals = await items
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Items = g.Count(),
                Good = g.Sum(i => i.GoodQuantity),
                Repairable = g.Sum(i => i.RepairableQuantity),
                Damaged = g.Sum(i => i.DamagedQuantity),
                Value = g.Sum(i => (i.ReplacementValue ?? i.PurchaseCost ?? 0) * i.GoodQuantity),
            })
            .FirstOrDefaultAsync(ct);

        var categories = await Db.PropCategories.AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new PropCategorySummaryDto(
                c.Id, c.Name,
                c.Items.Count(i => !i.IsDeleted),
                c.Items.Where(i => !i.IsDeleted).Sum(i => (int?)i.GoodQuantity) ?? 0,
                c.Items.Where(i => !i.IsDeleted).Sum(i => (int?)i.RepairableQuantity) ?? 0,
                c.Items.Where(i => !i.IsDeleted).Sum(i => (int?)i.DamagedQuantity) ?? 0,
                c.Items.Where(i => !i.IsDeleted)
                    .Sum(i => (decimal?)((i.ReplacementValue ?? i.PurchaseCost ?? 0) * i.GoodQuantity)) ?? 0))
            .ToListAsync(ct);

        var lowStock = await Base()
            .Where(i => i.ReorderLevel > 0 && i.GoodQuantity <= i.ReorderLevel)
            .OrderBy(i => i.GoodQuantity)
            .Take(10)
            .ToListAsync(ct);

        var overdue = await IssueBase().AsNoTracking()
            .Where(i => i.ExpectedReturnDate < today
                && (i.Status == PropIssueStatuses.Dispatched
                    || i.Status == PropIssueStatuses.PartiallyReturned))
            .OrderBy(i => i.ExpectedReturnDate)
            .Take(10)
            .ToListAsync(ct);

        var openIssues = await Db.PropIssues.CountAsync(
            i => i.Status == PropIssueStatuses.Draft || i.Status == PropIssueStatuses.Reserved, ct);

        var dispatched = await Db.PropIssues.CountAsync(
            i => i.Status == PropIssueStatuses.Dispatched
                || i.Status == PropIssueStatuses.PartiallyReturned, ct);

        var out_ = await Db.PropIssueLines
            .Where(l => l.Issue!.Status == PropIssueStatuses.Dispatched
                || l.Issue.Status == PropIssueStatuses.PartiallyReturned)
            .SumAsync(l => (int?)(l.IssuedQuantity - l.ReturnedQuantity
                - l.DamagedQuantity - l.LostQuantity - l.ConsumedQuantity), ct) ?? 0;

        var dispatchesThisWeek = await Db.PropIssues.CountAsync(
            i => i.DispatchDate >= today && i.DispatchDate <= weekEnd
                && i.Status != PropIssueStatuses.Cancelled, ct);

        var returnsDue = await Db.PropIssues.CountAsync(
            i => i.ExpectedReturnDate >= today && i.ExpectedReturnDate <= weekEnd
                && (i.Status == PropIssueStatuses.Dispatched
                    || i.Status == PropIssueStatuses.PartiallyReturned), ct);

        return Ok(new PropDashboardDto(
            totals?.Items ?? 0,
            (totals?.Good ?? 0) + (totals?.Repairable ?? 0) + (totals?.Damaged ?? 0),
            totals?.Good ?? 0,
            totals?.Repairable ?? 0,
            totals?.Damaged ?? 0,
            totals?.Value ?? 0,
            lowStock.Count,
            openIssues,
            dispatched,
            overdue.Count,
            Math.Max(0, out_),
            dispatchesThisWeek,
            returnsDue,
            categories,
            lowStock.Select(i => ToDto(i)).ToList(),
            overdue.Select(i => ToDto(i, null)).ToList()));
    }

    /* ------------------------------------------------------------------ *
     * Projection
     * ------------------------------------------------------------------ */

    private async Task<PropItemDto> GetDtoAsync(int id, CancellationToken ct)
    {
        var item = await Base().FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw ApiException.NotFound("Item");

        return ToDto(item);
    }

    private async Task<PropIssueDto> GetIssueDtoAsync(int id, CancellationToken ct)
    {
        var issue = await IssueBase().AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw ApiException.NotFound("Gate pass");

        return ToDto(issue, null);
    }

    private static string? PrimaryThumb(PropItem item) =>
        item.Photos.OrderBy(p => p.SortOrder).FirstOrDefault() is { } photo
            ? photo.ThumbnailUrl ?? photo.Url
            : null;

    private static PropItemDto ToDto(
        PropItem item, int? reserved = null, int? available = null)
    {
        var photos = item.Photos.OrderBy(p => p.SortOrder)
            .Select(p => new PropPhotoDto(p.Id, p.Url, p.ThumbnailUrl, p.Caption, p.SortOrder))
            .ToList();

        return new PropItemDto(
            item.Id, item.CategoryId, item.Category?.Name ?? "",
            item.StoreId, item.Store?.Name,
            item.Name, item.Code, item.ItemType, item.Status, item.Ownership,
            item.Size, item.Colour, item.Material, item.Unit, item.Description, item.Tags,
            item.GoodQuantity, item.RepairableQuantity, item.DamagedQuantity,
            item.OnHandQuantity, item.ReorderLevel, item.IsBelowReorderLevel,
            item.RentalRatePerDay, item.PurchaseCost, item.ReplacementValue,
            item.SupplierName, item.PurchaseDate,
            item.WeightKg, item.PackingUnit, item.IsFragile, item.IsSerialised,
            item.TurnaroundDays, item.StorageLocation,
            item.OwnerId, item.Owner?.Name,
            photos.FirstOrDefault()?.Url,
            photos.FirstOrDefault()?.ThumbnailUrl ?? photos.FirstOrDefault()?.Url,
            photos.Count, photos,
            reserved, available,
            item.CreatedAt, item.UpdatedAt);
    }

    /// <summary>
    /// The gate-pass projection, for the sibling controllers that share this
    /// resource — <see cref="PropKitsController"/> returns a pass after
    /// expanding a kit onto it, and there should only ever be one shape.
    /// </summary>
    internal static PropIssueDto Project(
        PropIssue issue, Dictionary<int, List<PropReservation>>? holds) => ToDto(issue, holds);

    private static PropIssueDto ToDto(
        PropIssue issue, Dictionary<int, List<PropReservation>>? holds)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var lines = issue.Lines.OrderBy(l => l.SortOrder).Select(l =>
        {
            int? free = null;

            if (holds is not null && l.Item is not null)
            {
                var reserved = (holds.GetValueOrDefault(l.PropItemId) ?? []).Sum(h => h.Quantity);
                free = Math.Max(0, l.Item.GoodQuantity - reserved);
            }

            return new PropIssueLineDto(
                l.Id, l.PropItemId, l.Item?.Name ?? "", l.Item?.Code ?? "",
                l.Item?.Category?.Name ?? "", l.Item?.Unit ?? "PCS",
                l.Item is null ? null : PrimaryThumb(l.Item),
                l.ReservedQuantity, l.IssuedQuantity, l.ReturnedQuantity,
                l.DamagedQuantity, l.LostQuantity, l.ConsumedQuantity, l.PendingQuantity,
                l.RatePerDay, l.ChargeableDays, l.LineTotal, l.Notes, l.SortOrder, free);
        }).ToList();

        return new PropIssueDto(
            issue.Id, issue.Code, issue.Status,
            issue.LeadId, issue.BookingId, issue.QuotationId, issue.ProjectId,
            issue.EventName, issue.EventType, issue.ClientName,
            issue.VenueName, issue.VenueAddress,
            issue.DispatchDate, issue.EventDate, issue.ExpectedReturnDate, issue.ActualReturnDate,
            issue.StoreId, issue.Store?.Name,
            issue.VehicleNumber, issue.DriverName, issue.DriverPhone,
            issue.SiteInChargeId, issue.SiteInCharge?.Name,
            issue.Notes, issue.DamageRecovery,
            issue.OwnerId, null,
            lines.Count,
            lines.Sum(l => l.ReservedQuantity),
            lines.Sum(l => l.IssuedQuantity),
            lines.Sum(l => l.ReturnedQuantity),
            lines.Sum(l => l.PendingQuantity),
            lines.Sum(l => l.LineTotal),
            issue.ExpectedReturnDate < today && issue.IsBlocking,
            issue.DispatchedAt, issue.ClosedAt, issue.CreatedAt,
            lines);
    }

    private async Task<string> NextItemCodeAsync(PropCategory category, CancellationToken ct)
    {
        // Sequential per category — PRP-BRASS-0007 — so a code read off a crate
        // says which shelf run it belongs to.
        var count = await Db.PropItems.CountAsync(i => i.CategoryId == category.Id, ct);

        for (var attempt = 0; attempt < 50; attempt++)
        {
            var code = $"{category.Code}-{count + 1 + attempt:D4}";
            if (!await Db.PropItems.AnyAsync(i => i.Code == code, ct)) return code;
        }

        return Code(category.Code);
    }
}
