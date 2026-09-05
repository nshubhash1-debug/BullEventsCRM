using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// The subcontracted half of every wedding — caterers, DJs, florists, mandap
/// decorators, bus operators.
///
/// A vendor is booked the way a prop is: over a window, against a capacity.
/// The difference is what the capacity means. A prop has forty pieces and a
/// booking takes twelve of them; a vendor has
/// <see cref="Vendor.ConcurrentEventCapacity"/> jobs they can run on one date,
/// and a purchase order takes one. Same overlap test, different unit.
///
/// Money is kept on the order rather than recomputed: <c>TotalCost</c> and
/// <c>TotalSell</c> are refreshed from the lines whenever the lines change, and
/// <c>AmountPaid</c> from the payments, so a list of two hundred orders does
/// not have to load every line to show what is owed.
/// </summary>
[ApiController]
[Route("api/vendors")]
[Authorize]
[SecuredBy(SecuredObjects.Unit)]
public class VendorsController(AppDbContext db) : CrmControllerBase(db)
{
    private static readonly FieldMap<Vendor> Fields = new FieldMap<Vendor>()
        .Text("name", "Vendor", searchable: true)
        .Text("code", "Code", searchable: true)
        .Select("status", "Status")
        .Text("services", "Services", searchable: true)
        .Text("contactPerson", "Contact", searchable: true)
        .Text("phone", "Phone", searchable: true)
        .Text("email", "Email", searchable: true)
        .Select("city", "City")
        .Text("coverageAreas", "Covers", searchable: true)
        .Text("gstNumber", "GST", searchable: true)
        .Number("paymentTermDays", "Payment terms")
        .Number("concurrentEventCapacity", "Jobs per date")
        .Number("rating", "Rating")
        .Number("completedEvents", "Events done")
        .Select("ownerName", "Owner", "Owner.Name")
        .Date("createdAt", "Added");

    private IQueryable<Vendor> Base() => Db.Vendors
        .Include(v => v.Owner)
        .Include(v => v.Rates)
        .Include(v => v.Documents)
        .AsNoTracking();

    /* ------------------------------------------------------------------ *
     * Directory
     * ------------------------------------------------------------------ */

    [HttpGet("fields")]
    public ActionResult<IReadOnlyList<FilterFieldDto>> FilterFields() =>
        Ok(DescribeFields(Fields, new Dictionary<string, IReadOnlyList<FilterOptionDto>>
        {
            ["status"] = Options(VendorStatuses.All),
        }));

    [HttpGet("meta")]
    public ActionResult<object> Meta() => Ok(new
    {
        services = ServiceCategories.All,
        statuses = VendorStatuses.All,
        rateBases = VendorRateBases.All,
        orderStatuses = PurchaseOrderStatuses.All,
    });

    [HttpPost("query")]
    public async Task<ActionResult<PagedResult<VendorDto>>> Query(
        QueryRequest request, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return Ok(await RunQueryAsync(
            Base(), request, Fields, v => ToDto(v, today),
            defaultSortPath: nameof(Vendor.Name), defaultSortDescending: false,
            cancellationToken: ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<VendorDto>> Get(int id, CancellationToken ct)
    {
        var vendor = await Base().FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw ApiException.NotFound("Vendor");

        return Ok(ToDto(vendor, DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost]
    public async Task<ActionResult<VendorDto>> Create(VendorInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw ApiException.BadRequest("A vendor name is required.");

        if (input.Services.Count == 0)
            throw ApiException.BadRequest("Say what this vendor supplies.");

        foreach (var service in input.Services)
            Require(service, ServiceCategories.All, "service");

        var code = string.IsNullOrWhiteSpace(input.Code)
            ? await NextCodeAsync(ct)
            : input.Code.Trim().ToUpperInvariant();

        if (await Db.Vendors.AnyAsync(v => v.Code == code, ct))
            throw ApiException.Conflict($"Vendor code {code} is already in use.");

        var vendor = new Vendor
        {
            CompanyId = Db.Tenant.CompanyId,
            Name = input.Name.Trim(),
            Code = code,
            Status = Require(input.Status, VendorStatuses.All, "status"),
            Services = string.Join(",", input.Services.Distinct()),
            ContactPerson = input.ContactPerson,
            Phone = input.Phone,
            AltPhone = input.AltPhone,
            Email = input.Email,
            Address = input.Address,
            City = input.City,
            CoverageAreas = input.CoverageAreas,
            Website = input.Website,
            GstNumber = input.GstNumber,
            PanNumber = input.PanNumber,
            BankAccountName = input.BankAccountName,
            BankAccountNumber = input.BankAccountNumber,
            BankIfsc = input.BankIfsc,
            PaymentTermDays = Math.Max(0, input.PaymentTermDays),
            AdvanceFraction = input.AdvanceFraction,
            ConcurrentEventCapacity = Math.Max(1, input.ConcurrentEventCapacity),
            Notes = input.Notes,
            OwnerId = input.OwnerId ?? (Db.Tenant.UserId > 0 ? Db.Tenant.UserId : null),
        };

        await RequireOwnerAsync(vendor.OwnerId, ct);
        Db.Vendors.Add(vendor);
        await Db.SaveChangesAsync(ct);

        return Ok(await GetDtoAsync(vendor.Id, ct));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<VendorDto>> Update(
        int id, VendorInput input, CancellationToken ct)
    {
        var vendor = await Db.Vendors.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw ApiException.NotFound("Vendor");

        foreach (var service in input.Services)
            Require(service, ServiceCategories.All, "service");

        vendor.Name = input.Name.Trim();
        vendor.Status = Require(input.Status, VendorStatuses.All, "status");
        vendor.Services = string.Join(",", input.Services.Distinct());
        vendor.ContactPerson = input.ContactPerson;
        vendor.Phone = input.Phone;
        vendor.AltPhone = input.AltPhone;
        vendor.Email = input.Email;
        vendor.Address = input.Address;
        vendor.City = input.City;
        vendor.CoverageAreas = input.CoverageAreas;
        vendor.Website = input.Website;
        vendor.GstNumber = input.GstNumber;
        vendor.PanNumber = input.PanNumber;
        vendor.BankAccountName = input.BankAccountName;
        vendor.BankAccountNumber = input.BankAccountNumber;
        vendor.BankIfsc = input.BankIfsc;
        vendor.PaymentTermDays = Math.Max(0, input.PaymentTermDays);
        vendor.AdvanceFraction = input.AdvanceFraction;
        vendor.ConcurrentEventCapacity = Math.Max(1, input.ConcurrentEventCapacity);
        vendor.Notes = input.Notes;

        if (input.OwnerId != vendor.OwnerId)
        {
            await RequireOwnerAsync(input.OwnerId, ct);
            vendor.OwnerId = input.OwnerId;
        }

        await Db.SaveChangesAsync(ct);
        return Ok(await GetDtoAsync(vendor.Id, ct));
    }

    [PermissionAction(ObjectAction.Delete)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var vendor = await Db.Vendors.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw ApiException.NotFound("Vendor");

        var live = await Db.VendorPurchaseOrders.AnyAsync(
            o => o.VendorId == id
                && (o.Status == PurchaseOrderStatuses.Sent
                    || o.Status == PurchaseOrderStatuses.Confirmed), ct);

        if (live)
            throw ApiException.BadRequest(
                "This vendor has live orders. Close or cancel them first.");

        SoftDelete(vendor);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    /* ---------------- rate card ---------------- */

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/rates")]
    public async Task<ActionResult<VendorDto>> AddRate(
        int id, VendorRateInput input, CancellationToken ct)
    {
        var vendor = await Db.Vendors.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw ApiException.NotFound("Vendor");

        if (string.IsNullOrWhiteSpace(input.Name))
            throw ApiException.BadRequest("Say what this rate is for.");

        if (input.Rate < 0)
            throw ApiException.BadRequest("A rate cannot be negative.");

        Db.VendorRates.Add(new VendorRate
        {
            CompanyId = vendor.CompanyId,
            VendorId = vendor.Id,
            Service = Require(input.Service, ServiceCategories.All, "service"),
            Name = input.Name.Trim(),
            Basis = Require(input.Basis, VendorRateBases.All, "rate basis"),
            Rate = input.Rate,
            SellRate = input.SellRate,
            MinimumQuantity = input.MinimumQuantity,
            Notes = input.Notes,
            IsActive = input.IsActive,
        });

        await Db.SaveChangesAsync(ct);
        return Ok(await GetDtoAsync(vendor.Id, ct));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPut("rates/{rateId:int}")]
    public async Task<ActionResult<VendorDto>> UpdateRate(
        int rateId, VendorRateInput input, CancellationToken ct)
    {
        var rate = await Db.VendorRates.FirstOrDefaultAsync(r => r.Id == rateId, ct)
            ?? throw ApiException.NotFound("Rate");

        rate.Service = Require(input.Service, ServiceCategories.All, "service");
        rate.Name = input.Name.Trim();
        rate.Basis = Require(input.Basis, VendorRateBases.All, "rate basis");
        rate.Rate = input.Rate;
        rate.SellRate = input.SellRate;
        rate.MinimumQuantity = input.MinimumQuantity;
        rate.Notes = input.Notes;
        rate.IsActive = input.IsActive;

        await Db.SaveChangesAsync(ct);
        return Ok(await GetDtoAsync(rate.VendorId, ct));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpDelete("rates/{rateId:int}")]
    public async Task<ActionResult<VendorDto>> DeleteRate(int rateId, CancellationToken ct)
    {
        var rate = await Db.VendorRates.FirstOrDefaultAsync(r => r.Id == rateId, ct)
            ?? throw ApiException.NotFound("Rate");

        var vendorId = rate.VendorId;
        Db.VendorRates.Remove(rate);
        await Db.SaveChangesAsync(ct);

        return Ok(await GetDtoAsync(vendorId, ct));
    }

    /* ---------------- documents ---------------- */

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/documents")]
    public async Task<ActionResult<VendorDto>> AddDocument(
        int id, VendorDocumentInput input, CancellationToken ct)
    {
        var vendor = await Db.Vendors.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw ApiException.NotFound("Vendor");

        Db.VendorDocuments.Add(new VendorDocument
        {
            CompanyId = vendor.CompanyId,
            VendorId = vendor.Id,
            DocumentType = input.DocumentType.Trim(),
            FileName = input.FileName.Trim(),
            Url = input.Url,
            IssueDate = input.IssueDate,
            ExpiryDate = input.ExpiryDate,
            Notes = input.Notes,
        });

        await Db.SaveChangesAsync(ct);
        return Ok(await GetDtoAsync(vendor.Id, ct));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpDelete("documents/{documentId:int}")]
    public async Task<IActionResult> DeleteDocument(int documentId, CancellationToken ct)
    {
        var document = await Db.VendorDocuments.FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw ApiException.NotFound("Document");

        Db.VendorDocuments.Remove(document);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Licences and insurance lapsing soon. The sweep nobody remembers to run.</summary>
    [HttpGet("documents/expiring")]
    public async Task<ActionResult<IReadOnlyList<object>>> ExpiringDocuments(
        [FromQuery] int withinDays = 60, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = today.AddDays(Math.Clamp(withinDays, 1, 365));

        var rows = await Db.VendorDocuments.AsNoTracking()
            .Include(d => d.Vendor)
            .Where(d => d.ExpiryDate != null && d.ExpiryDate <= horizon)
            .OrderBy(d => d.ExpiryDate)
            .Take(100)
            .Select(d => new
            {
                d.Id,
                d.VendorId,
                VendorName = d.Vendor!.Name,
                d.Vendor.Phone,
                d.DocumentType,
                d.FileName,
                d.ExpiryDate,
                IsExpired = d.ExpiryDate < today,
            })
            .ToListAsync(ct);

        return Ok(rows);
    }

    /* ------------------------------------------------------------------ *
     * Availability
     * ------------------------------------------------------------------ */

    /// <summary>
    /// How many blocking orders each vendor already carries over a window.
    ///
    /// Same shape as the props holds lookup, and for the same reason: the count
    /// feeds the directory, the picker and the order screen, and each has its
    /// own vendor query already.
    /// </summary>
    private async Task<Dictionary<int, int>> CommitmentsAsync(
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<int>? vendorIds,
        int? excludeOrderId,
        CancellationToken ct)
    {
        var query = Db.VendorPurchaseOrders.AsNoTracking()
            .Where(o => PurchaseOrderStatuses.Blocking.Contains(o.Status))
            .Where(o => o.ServiceDate <= to && o.ServiceEndDate >= from);

        if (vendorIds is { Count: > 0 })
            query = query.Where(o => vendorIds.Contains(o.VendorId));

        if (excludeOrderId is int exclude)
            query = query.Where(o => o.Id != exclude);

        return await query
            .GroupBy(o => o.VendorId)
            .Select(g => new { VendorId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.VendorId, x => x.Count, ct);
    }

    [HttpPost("availability")]
    public async Task<ActionResult<IReadOnlyList<VendorDto>>> Availability(
        VendorAvailabilityRequest request, CancellationToken ct)
    {
        if (request.To < request.From)
            throw ApiException.BadRequest("The end date cannot fall before the start date.");

        var query = Base().Where(v => v.Status == VendorStatuses.Active
            || v.Status == VendorStatuses.OnWatch);

        if (!string.IsNullOrWhiteSpace(request.Service))
        {
            // Wrapped in separators on both sides so "Decor" cannot also match a
            // hypothetical "DecorLighting" sitting in the same list.
            //
            // Folded to lower and re-collated to "C" because every string column
            // here carries the app's non-deterministic ICU collation, and
            // Postgres refuses LIKE against one outright — the same fix
            // QueryEngine.CaseInsensitiveText applies to every generated filter.
            var needle = "," + request.Service.Trim().ToLowerInvariant() + ",";
            query = query.Where(v =>
                EF.Functions.Collate(("," + v.Services + ",").ToLower(), "C").Contains(needle));
        }

        if (!string.IsNullOrWhiteSpace(request.City))
        {
            var city = request.City.Trim();
            var cityNeedle = city.ToLowerInvariant();

            query = query.Where(v => v.City == city
                || (v.CoverageAreas != null
                    && EF.Functions.Collate(v.CoverageAreas.ToLower(), "C").Contains(cityNeedle)));
        }

        var vendors = await query.OrderBy(v => v.Name).Take(300).ToListAsync(ct);
        var ids = vendors.Select(v => v.Id).ToList();
        var committed = await CommitmentsAsync(
            request.From, request.To, ids, request.ExcludePurchaseOrderId, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Free ones first, then by rating — a picker should open on who can
        // actually take the job, best first.
        return Ok(vendors
            .Select(v => ToDto(v, today, committed.GetValueOrDefault(v.Id)))
            .OrderByDescending(v => v.IsAvailable == true)
            .ThenByDescending(v => v.Rating ?? 0)
            .ThenBy(v => v.Name)
            .ToList());
    }

    /* ------------------------------------------------------------------ *
     * Purchase orders
     * ------------------------------------------------------------------ */

    private IQueryable<VendorPurchaseOrder> OrderBase() => Db.VendorPurchaseOrders
        .Include(o => o.Vendor)
        .Include(o => o.Coordinator)
        .Include(o => o.Lines)
        .Include(o => o.Payments);

    [HttpGet("orders")]
    public async Task<ActionResult<IReadOnlyList<PurchaseOrderDto>>> Orders(
        [FromQuery] string? status,
        [FromQuery] int? vendorId,
        [FromQuery] int? leadId,
        [FromQuery] bool unpaidOnly = false,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        var query = OrderBase().AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(o => o.Status == status);
        if (vendorId is int vid) query = query.Where(o => o.VendorId == vid);
        if (leadId is int lid) query = query.Where(o => o.LeadId == lid);

        if (unpaidOnly)
        {
            query = query.Where(o => o.AmountPaid < o.TotalCost
                && o.Status != PurchaseOrderStatuses.Cancelled);
        }

        var rows = await query
            .OrderByDescending(o => o.ServiceDate).ThenByDescending(o => o.Id)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(ct);

        return Ok(rows.Select(ToDto).ToList());
    }

    [HttpGet("orders/{id:int}")]
    public async Task<ActionResult<PurchaseOrderDto>> GetOrder(int id, CancellationToken ct)
    {
        var order = await OrderBase().AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw ApiException.NotFound("Purchase order");

        return Ok(ToDto(order));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("orders")]
    public async Task<ActionResult<PurchaseOrderDto>> CreateOrder(
        PurchaseOrderInput input, CancellationToken ct)
    {
        var vendor = await Db.Vendors.FirstOrDefaultAsync(v => v.Id == input.VendorId, ct)
            ?? throw ApiException.BadRequest("Select a valid vendor.");

        if (!vendor.IsBookable)
            throw ApiException.BadRequest(
                $"{vendor.Name} is {vendor.Status.ToLowerInvariant()} and cannot be booked.");

        var end = input.ServiceEndDate ?? input.ServiceDate;
        if (end < input.ServiceDate)
            throw ApiException.BadRequest("The end date cannot fall before the service date.");

        var order = new VendorPurchaseOrder
        {
            CompanyId = Db.Tenant.CompanyId,
            Code = Code("PO"),
            VendorId = vendor.Id,
            Status = PurchaseOrderStatuses.Draft,
            Service = Require(input.Service, ServiceCategories.All, "service"),
            LeadId = input.LeadId,
            BookingId = input.BookingId,
            QuotationId = input.QuotationId,
            ProjectId = input.ProjectId,
            EventName = input.EventName,
            EventType = input.EventType,
            ClientName = input.ClientName,
            VenueName = input.VenueName,
            VenueAddress = input.VenueAddress,
            GuestCount = input.GuestCount,
            ServiceDate = input.ServiceDate,
            ServiceEndDate = end,
            ReportingTime = input.ReportingTime,
            RetentionAmount = input.RetentionAmount,
            Terms = input.Terms,
            Notes = input.Notes,
            CoordinatorId = input.CoordinatorId,
            OwnerId = input.OwnerId ?? (Db.Tenant.UserId > 0 ? Db.Tenant.UserId : null),
        };

        if (input.LeadId is int leadId)
        {
            var lead = await Db.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == leadId, ct)
                ?? throw ApiException.BadRequest("Select a valid lead.");

            order.ClientName ??= lead.Name;
            order.EventType ??= lead.EventType;
        }

        if (input.ProjectId is int projectId)
        {
            var project = await Db.Projects.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == projectId, ct)
                ?? throw ApiException.BadRequest("Select a valid venue.");

            order.VenueName ??= project.Name;
            order.VenueAddress ??= project.Address;
        }

        await RequireOwnerAsync(order.OwnerId, ct);

        foreach (var (line, index) in (input.Lines ?? []).Select((l, n) => (l, n)))
        {
            order.Lines.Add(BuildLine(line, index));
        }

        Recalculate(order);
        Db.VendorPurchaseOrders.Add(order);
        await Db.SaveChangesAsync(ct);

        return Ok(await GetOrderDtoAsync(order.Id, ct));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPut("orders/{id:int}")]
    public async Task<ActionResult<PurchaseOrderDto>> UpdateOrder(
        int id, PurchaseOrderInput input, CancellationToken ct)
    {
        var order = await Db.VendorPurchaseOrders.Include(o => o.Lines).Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw ApiException.NotFound("Purchase order");

        if (order.Status is PurchaseOrderStatuses.Closed or PurchaseOrderStatuses.Cancelled)
            throw ApiException.BadRequest(
                $"A {order.Status.ToLowerInvariant()} order cannot be edited.");

        var end = input.ServiceEndDate ?? input.ServiceDate;
        if (end < input.ServiceDate)
            throw ApiException.BadRequest("The end date cannot fall before the service date.");

        order.Service = Require(input.Service, ServiceCategories.All, "service");
        order.EventName = input.EventName;
        order.EventType = input.EventType;
        order.ClientName = input.ClientName;
        order.VenueName = input.VenueName;
        order.VenueAddress = input.VenueAddress;
        order.GuestCount = input.GuestCount;
        order.ServiceDate = input.ServiceDate;
        order.ServiceEndDate = end;
        order.ReportingTime = input.ReportingTime;
        order.RetentionAmount = input.RetentionAmount;
        order.Terms = input.Terms;
        order.Notes = input.Notes;
        order.CoordinatorId = input.CoordinatorId;

        await Db.SaveChangesAsync(ct);
        return Ok(await GetOrderDtoAsync(order.Id, ct));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("orders/{id:int}/lines")]
    public async Task<ActionResult<PurchaseOrderDto>> AddOrderLine(
        int id, VendorPoLineInput input, CancellationToken ct)
    {
        var order = await Db.VendorPurchaseOrders.Include(o => o.Lines).Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw ApiException.NotFound("Purchase order");

        if (order.Status is PurchaseOrderStatuses.Closed or PurchaseOrderStatuses.Cancelled)
            throw ApiException.BadRequest("This order is finished.");

        var sortOrder = order.Lines.Count == 0 ? 0 : order.Lines.Max(l => l.SortOrder) + 1;
        order.Lines.Add(BuildLine(input, sortOrder));

        Recalculate(order);
        await Db.SaveChangesAsync(ct);

        return Ok(await GetOrderDtoAsync(order.Id, ct));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpDelete("orders/{id:int}/lines/{lineId:int}")]
    public async Task<ActionResult<PurchaseOrderDto>> RemoveOrderLine(
        int id, int lineId, CancellationToken ct)
    {
        var order = await Db.VendorPurchaseOrders.Include(o => o.Lines).Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw ApiException.NotFound("Purchase order");

        var line = order.Lines.FirstOrDefault(l => l.Id == lineId)
            ?? throw ApiException.NotFound("Line");

        order.Lines.Remove(line);
        Db.VendorPoLines.Remove(line);

        Recalculate(order);
        await Db.SaveChangesAsync(ct);

        return Ok(await GetOrderDtoAsync(order.Id, ct));
    }

    private static VendorPoLine BuildLine(VendorPoLineInput input, int sortOrder)
    {
        if (input.Quantity <= 0)
            throw ApiException.BadRequest("Quantity must be more than zero.");

        if (input.Rate < 0)
            throw ApiException.BadRequest("A rate cannot be negative.");

        return new VendorPoLine
        {
            VendorRateId = input.VendorRateId,
            Description = input.Description.Trim(),
            Basis = input.Basis,
            Quantity = input.Quantity,
            Rate = input.Rate,
            SellRate = input.SellRate,
            Notes = input.Notes,
            SortOrder = sortOrder,
        };
    }

    /// <summary>Refreshes the stored totals from the lines and the payments.</summary>
    private static void Recalculate(VendorPurchaseOrder order)
    {
        order.TotalCost = order.Lines.Sum(l => l.LineCost);
        order.TotalSell = order.Lines.Sum(l => l.LineSell);
        order.AmountPaid = order.Payments.Sum(p => p.Amount);
    }

    /* ---------------- moving an order along ---------------- */

    /// <summary>
    /// Advances an order, refusing the moves that would lose money or
    /// double-book a supplier.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("orders/{id:int}/status")]
    public async Task<ActionResult<PurchaseOrderDto>> SetOrderStatus(
        int id,
        [FromQuery] string status,
        [FromQuery] int? rating,
        CancellationToken ct)
    {
        var order = await Db.VendorPurchaseOrders
            .Include(o => o.Vendor).Include(o => o.Lines).Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw ApiException.NotFound("Purchase order");

        var target = Require(status, PurchaseOrderStatuses.All, "status");

        if (order.Status == target)
            throw ApiException.BadRequest($"This order is already {target.ToLowerInvariant()}.");

        if (order.Status is PurchaseOrderStatuses.Closed)
            throw ApiException.BadRequest("A closed order cannot be reopened.");

        // Capacity is only checked on the way *into* a blocking state. An order
        // already holding the date is not competing with itself.
        if (PurchaseOrderStatuses.Blocking.Contains(target) && !order.IsBlocking)
        {
            var vendor = order.Vendor
                ?? throw ApiException.BadRequest("This order's vendor no longer exists.");

            var committed = await CommitmentsAsync(
                order.ServiceDate, order.ServiceEndDate, [vendor.Id], order.Id, ct);

            var taken = committed.GetValueOrDefault(vendor.Id);
            if (taken >= vendor.ConcurrentEventCapacity)
            {
                throw ApiException.BadRequest(
                    $"{vendor.Name} already has {taken} job(s) between " +
                    $"{order.ServiceDate:dd MMM} and {order.ServiceEndDate:dd MMM}, " +
                    $"and can run {vendor.ConcurrentEventCapacity} at a time.");
            }
        }

        if (target == PurchaseOrderStatuses.Closed && order.AmountDue > 0)
            throw ApiException.BadRequest(
                $"{order.AmountDue:N0} is still owed on this order. Record the payment first.");

        order.Status = target;

        switch (target)
        {
            case PurchaseOrderStatuses.Sent:
                order.SentAt ??= DateTime.UtcNow;
                break;

            case PurchaseOrderStatuses.Confirmed:
                order.ConfirmedAt ??= DateTime.UtcNow;
                await LogLeadActivityAsync(
                    order.LeadId, LeadActivityTypes.Note,
                    $"{order.Vendor?.Name} confirmed for {order.Service} on " +
                    $"{order.ServiceDate:dd MMM yyyy} ({order.Code}).", ct);
                break;

            case PurchaseOrderStatuses.Delivered:
                order.DeliveredAt ??= DateTime.UtcNow;
                if (rating is int score)
                {
                    order.Rating = Math.Clamp(score, 1, 5);
                }
                await RecordVendorPerformanceAsync(order, ct);
                break;
        }

        await Db.SaveChangesAsync(ct);
        return Ok(await GetOrderDtoAsync(order.Id, ct));
    }

    /// <summary>
    /// Rolls a delivered order's rating into the vendor's running average.
    ///
    /// Kept as a running mean over <c>CompletedEvents</c> rather than recomputed
    /// across every past order, so the directory can sort by rating without a
    /// subquery per row.
    /// </summary>
    private async Task RecordVendorPerformanceAsync(
        VendorPurchaseOrder order, CancellationToken ct)
    {
        var vendor = await Db.Vendors.FirstOrDefaultAsync(v => v.Id == order.VendorId, ct);
        if (vendor is null) return;

        if (order.Rating is int score)
        {
            var previous = (vendor.Rating ?? 0) * vendor.CompletedEvents;
            vendor.Rating = Math.Round((previous + score) / (vendor.CompletedEvents + 1), 2);
        }

        vendor.CompletedEvents += 1;
    }

    /* ---------------- payments ---------------- */

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("orders/{id:int}/payments")]
    public async Task<ActionResult<PurchaseOrderDto>> AddPayment(
        int id, VendorPaymentInput input, CancellationToken ct)
    {
        var order = await Db.VendorPurchaseOrders.Include(o => o.Lines).Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw ApiException.NotFound("Purchase order");

        if (input.Amount <= 0)
            throw ApiException.BadRequest("A payment must be more than zero.");

        if (order.Status == PurchaseOrderStatuses.Cancelled)
            throw ApiException.BadRequest("This order is cancelled.");

        var afterwards = order.AmountPaid + input.Amount;
        if (afterwards > order.TotalCost)
        {
            throw ApiException.BadRequest(
                $"That would pay {afterwards:N0} against an order of {order.TotalCost:N0}. " +
                "Raise the order value first if the scope grew.");
        }

        order.Payments.Add(new VendorPayment
        {
            CompanyId = order.CompanyId,
            VendorPurchaseOrderId = order.Id,
            Amount = input.Amount,
            PaidOn = input.PaidOn,
            Mode = input.Mode,
            Kind = input.Kind,
            Reference = input.Reference,
            TdsAmount = input.TdsAmount,
            Notes = input.Notes,
        });

        Recalculate(order);
        await Db.SaveChangesAsync(ct);

        return Ok(await GetOrderDtoAsync(order.Id, ct));
    }

    /* ------------------------------------------------------------------ *
     * Projection
     * ------------------------------------------------------------------ */

    private async Task<VendorDto> GetDtoAsync(int id, CancellationToken ct)
    {
        var vendor = await Base().FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw ApiException.NotFound("Vendor");

        return ToDto(vendor, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    private async Task<PurchaseOrderDto> GetOrderDtoAsync(int id, CancellationToken ct)
    {
        var order = await OrderBase().AsNoTracking().FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw ApiException.NotFound("Purchase order");

        return ToDto(order);
    }

    private async Task<string> NextCodeAsync(CancellationToken ct)
    {
        var count = await Db.Vendors.IgnoreQueryFilters()
            .CountAsync(v => v.CompanyId == Db.Tenant.CompanyId, ct);

        for (var attempt = 0; attempt < 50; attempt++)
        {
            var code = $"VN-{count + 1 + attempt:D4}";
            if (!await Db.Vendors.AnyAsync(v => v.Code == code, ct)) return code;
        }

        return Code("VN");
    }

    internal static VendorDto ToDto(Vendor vendor, DateOnly today, int? committed = null)
    {
        var services = string.IsNullOrWhiteSpace(vendor.Services)
            ? []
            : vendor.Services
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList();

        var rates = vendor.Rates.Where(r => r.IsActive)
            .OrderBy(r => r.Service).ThenBy(r => r.Name)
            .Select(r => new VendorRateDto(
                r.Id, r.VendorId, r.Service, r.Name, r.Basis, r.Rate, r.SellRate,
                r.MarginPerUnit, r.MinimumQuantity, r.Notes, r.IsActive))
            .ToList();

        var documents = vendor.Documents
            .OrderBy(d => d.ExpiryDate ?? DateOnly.MaxValue)
            .Select(d => new VendorDocumentDto(
                d.Id, d.DocumentType, d.FileName, d.Url, d.IssueDate, d.ExpiryDate,
                d.IsExpired(today), d.Notes))
            .ToList();

        return new VendorDto(
            vendor.Id, vendor.Name, vendor.Code, vendor.Status, services,
            vendor.ContactPerson, vendor.Phone, vendor.AltPhone, vendor.Email,
            vendor.Address, vendor.City, vendor.CoverageAreas, vendor.Website,
            vendor.GstNumber, vendor.PanNumber,
            vendor.BankAccountName, vendor.BankAccountNumber, vendor.BankIfsc,
            vendor.PaymentTermDays, vendor.AdvanceFraction,
            vendor.ConcurrentEventCapacity, vendor.Rating, vendor.CompletedEvents,
            vendor.Notes, vendor.OwnerId, vendor.Owner?.Name,
            rates.Count,
            rates.Count == 0 ? null : rates.Min(r => r.Rate),
            documents.Count(d => d.ExpiryDate is not null && d.ExpiryDate <= today.AddDays(60)),
            committed,
            committed is null ? null : committed < vendor.ConcurrentEventCapacity,
            rates, documents,
            vendor.CreatedAt, vendor.UpdatedAt);
    }

    internal static PurchaseOrderDto ToDto(VendorPurchaseOrder order)
    {
        var lines = order.Lines.OrderBy(l => l.SortOrder)
            .Select(l => new VendorPoLineDto(
                l.Id, l.VendorRateId, l.Description, l.Basis, l.Quantity, l.Rate,
                l.SellRate, l.LineCost, l.LineSell, l.Notes, l.SortOrder))
            .ToList();

        var payments = order.Payments.OrderByDescending(p => p.PaidOn)
            .Select(p => new VendorPaymentDto(
                p.Id, p.Amount, p.PaidOn, p.Mode, p.Reference, p.Kind, p.TdsAmount, p.Notes))
            .ToList();

        return new PurchaseOrderDto(
            order.Id, order.Code, order.Status,
            order.VendorId, order.Vendor?.Name ?? "", order.Vendor?.Phone,
            order.Service,
            order.LeadId, order.BookingId, order.QuotationId, order.ProjectId,
            order.EventName, order.EventType, order.ClientName,
            order.VenueName, order.VenueAddress, order.GuestCount,
            order.ServiceDate, order.ServiceEndDate, order.ReportingTime,
            order.TotalCost, order.TotalSell, order.Margin,
            order.AmountPaid, order.AmountDue, order.RetentionAmount,
            order.Rating, order.Terms, order.Notes,
            order.CoordinatorId, order.Coordinator?.Name,
            lines.Count,
            order.SentAt, order.ConfirmedAt, order.DeliveredAt, order.CreatedAt,
            lines, payments);
    }
}
