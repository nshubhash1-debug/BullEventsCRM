namespace BullEvents.Api.Models;

/* ------------------------------------------------------------------ *
 * Rentable asset inventory — props, décor, furniture, fabric, florals.
 *
 * Distinct from the venue inventory in Inventory.cs, and deliberately so.
 * A venue is one bookable thing sold by the date; a prop is a *countable*
 * thing sold by the piece, and the same forty gold vases go out to a sangeet
 * on the 12th, come back on the 13th, and go out again on the 14th.
 *
 * That difference decides the whole shape of this file:
 *
 *   - a prop's Status is its standing in the catalogue, never "free on the
 *     14th" — availability is a sum over <see cref="PropReservation"/> rows
 *     that overlap the window, exactly the mistake SpaceBooking exists to
 *     avoid on the venue side;
 *   - stock is never edited in place. Every change is a
 *     <see cref="PropStockMovement"/> row, and the on-hand figure is the
 *     ledger's running total. A godown argument six months later is settled
 *     by reading the ledger, not by trusting a number somebody typed.
 * ------------------------------------------------------------------ */

/// <summary>
/// What kind of stock a catalogue line is, which decides how it behaves when
/// it comes back from an event.
///
/// The split that actually matters is returnable versus consumed: a brass urli
/// is checked back in and its quantity restored, while a bag of rose petals is
/// gone the moment it ships. Everything else here is a reporting bucket.
/// </summary>
public static class PropItemTypes
{
    /// <summary>Décor pieces — pots, lamps, mirrors, installations.</summary>
    public const string Prop = "Prop";

    /// <summary>Sofas, chairs, tables, consoles.</summary>
    public const string Furniture = "Furniture";

    /// <summary>Curtains, canopies, chair covers, table tops — soft goods.</summary>
    public const string Fabric = "Fabric";

    /// <summary>Sound, light, AV, generators, cooling. Usually serialised.</summary>
    public const string Equipment = "Equipment";

    /// <summary>Fresh and artificial florals.</summary>
    public const string Floral = "Floral";

    /// <summary>Mandap, stage, truss and other built structures.</summary>
    public const string Structure = "Structure";

    /// <summary>Candles, petals, fuel — used up rather than returned.</summary>
    public const string Consumable = "Consumable";

    public static readonly string[] All =
    [
        Prop, Furniture, Fabric, Equipment, Floral, Structure, Consumable
    ];

    /// <summary>
    /// Whether stock of this type comes back after the event.
    ///
    /// A consumable's issue is a permanent deduction; everything else is a
    /// loan that the return leg puts back on the shelf.
    /// </summary>
    public static bool IsReturnable(string? type) => type != Consumable;
}

/// <summary>
/// The state a physical piece is in.
///
/// Held as three counts on the item rather than one status, because a line in
/// the godown register is a quantity, not an object: "34 black candle stands,
/// 31 good, 2 repairable, 1 broken" is one row, and splitting it into 34 rows
/// to give each a status would make the catalogue unreadable.
/// </summary>
public static class PropConditions
{
    public const string Good = "Good";
    public const string Repairable = "Repairable";
    public const string Damaged = "Damaged";
    public const string Lost = "Lost";

    public static readonly string[] All = [Good, Repairable, Damaged, Lost];
}

/// <summary>Whether the company owns the stock or hires it in for the job.</summary>
public static class PropOwnershipTypes
{
    /// <summary>On the books, in our godown.</summary>
    public const string Owned = "Owned";

    /// <summary>Sub-hired from another vendor for a specific event.</summary>
    public const string SubHired = "SubHired";

    /// <summary>The client's own property, held by us for the duration.</summary>
    public const string ClientSupplied = "ClientSupplied";

    public static readonly string[] All = [Owned, SubHired, ClientSupplied];
}

/// <summary>The catalogue standing of a line — not its availability on a date.</summary>
public static class PropItemStatuses
{
    public const string Active = "Active";

    /// <summary>Out of service — under repair, awaiting a decision.</summary>
    public const string OnHold = "OnHold";

    /// <summary>Kept on the books for history, never offered on a new quote.</summary>
    public const string Retired = "Retired";

    public static readonly string[] All = [Active, OnHold, Retired];
}

/// <summary>
/// Why stock moved. The ledger's verb.
///
/// <see cref="Delta"/> is the single place that decides whether a movement adds
/// to or removes from on-hand stock, so a new movement type cannot be
/// introduced without saying which way it counts.
/// </summary>
public static class PropMovementTypes
{
    /// <summary>The count the item entered the system with.</summary>
    public const string Opening = "Opening";

    public const string Purchase = "Purchase";

    /// <summary>Dispatched to an event.</summary>
    public const string IssueOut = "IssueOut";

    /// <summary>Came back from an event.</summary>
    public const string Return = "Return";

    /// <summary>Used up on site and not coming back.</summary>
    public const string Consumed = "Consumed";

    /// <summary>Came back broken. Moves the piece from good to damaged.</summary>
    public const string Damage = "Damage";

    /// <summary>Repaired. Moves the piece from repairable back to good.</summary>
    public const string Repair = "Repair";

    /// <summary>Written off the books entirely.</summary>
    public const string WriteOff = "WriteOff";

    /// <summary>Did not come back. Usually billed to the event.</summary>
    public const string Lost = "Lost";

    /// <summary>A physical count corrected the book figure.</summary>
    public const string Adjustment = "Adjustment";

    public const string TransferIn = "TransferIn";
    public const string TransferOut = "TransferOut";

    public static readonly string[] All =
    [
        Opening, Purchase, IssueOut, Return, Consumed, Damage, Repair,
        WriteOff, Lost, Adjustment, TransferIn, TransferOut
    ];

    /// <summary>
    /// Which way a movement pushes the on-hand count, as a sign.
    ///
    /// Damage and Repair return 0 deliberately: they reclassify a piece between
    /// the condition buckets without changing how many pieces exist. Adjustment
    /// also returns 0 because its row carries a signed quantity of its own — a
    /// stock count can go either way.
    /// </summary>
    public static int Delta(string type) => type switch
    {
        Opening or Purchase or Return or TransferIn => 1,
        IssueOut or Consumed or WriteOff or Lost or TransferOut => -1,
        _ => 0,
    };
}

/// <summary>
/// Where a gate pass is in its life.
///
/// The states that block stock are Reserved through PartiallyReturned — see
/// <see cref="PropIssue.IsBlocking"/>. Draft does not block, which is what lets
/// a coordinator build next month's list without freezing stock somebody needs
/// this weekend.
/// </summary>
public static class PropIssueStatuses
{
    public const string Draft = "Draft";

    /// <summary>Committed against the dates, still in the godown.</summary>
    public const string Reserved = "Reserved";

    /// <summary>Loaded and gone.</summary>
    public const string Dispatched = "Dispatched";

    /// <summary>Some of it is back, some is not.</summary>
    public const string PartiallyReturned = "PartiallyReturned";

    public const string Returned = "Returned";

    /// <summary>Reconciled — shortages billed, damages booked.</summary>
    public const string Closed = "Closed";

    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
    [
        Draft, Reserved, Dispatched, PartiallyReturned, Returned, Closed, Cancelled
    ];

    /// <summary>Statuses in which the pass still holds stock away from other events.</summary>
    public static readonly string[] Blocking =
    [
        Reserved, Dispatched, PartiallyReturned
    ];
}

/* ------------------------------------------------------------------ *
 * Catalogue
 * ------------------------------------------------------------------ */

/// <summary>
/// A godown, a container, or a shelf run. Where the stock physically sits.
///
/// Flat rather than nested: a decorator's storage is two or three sheds, and a
/// tree would cost more in UI than it saves in accuracy.
/// </summary>
public class PropStore : ITenantScoped, ISoftDeletable, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    public string? City { get; set; }
    public string? Address { get; set; }

    /// <summary>Godown in-charge. A user, so the app can route return chasers.</summary>
    public int? KeeperId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    public User? Keeper { get; set; }
}

/// <summary>
/// A catalogue section — "Brass items", "Glass props &amp; mirror".
///
/// One level of nesting is supported through <see cref="ParentId"/> so a large
/// section can be split later without a migration, but the imported set is flat
/// and the UI reads it that way.
/// </summary>
public class PropCategory : ITenantScoped, ISoftDeletable, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Short slug used on item codes — BRASS, GLASS, CANOPY.</summary>
    public string Code { get; set; } = string.Empty;

    public int? ParentId { get; set; }

    /// <summary>Default type for items created in this section.</summary>
    public string DefaultItemType { get; set; } = PropItemTypes.Prop;

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    public PropCategory? Parent { get; set; }
    public ICollection<PropItem> Items { get; set; } = new List<PropItem>();
}

/// <summary>
/// One line of the godown register — a countable kind of thing, not a single
/// physical object.
///
/// "Golden vase (glass), 11 inch — 9 pcs" is one item with a quantity of nine.
/// Serial-level tracking would be right for a mixing desk and wrong for nine
/// identical vases, so it is offered as an option
/// (<see cref="IsSerialised"/>) rather than imposed on everything.
///
/// The three condition counts are the stored truth; <see cref="OnHandQuantity"/>
/// is their sum, and <see cref="AvailableQuantity"/> is what is left after the
/// reservations for a given window — which is why availability is computed by
/// the controller against dates, and never cached on this row.
/// </summary>
public class PropItem : ITenantScoped, ISoftDeletable, IAuditable, IOwnedRecord
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int CategoryId { get; set; }

    /// <summary>Where it normally lives. Null while a company runs one godown.</summary>
    public int? StoreId { get; set; }

    /// <summary>The register's own wording, kept verbatim from the sheet.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Stock code — unique per company, quotable on a gate pass.</summary>
    public string Code { get; set; } = string.Empty;

    public string ItemType { get; set; } = PropItemTypes.Prop;
    public string Status { get; set; } = PropItemStatuses.Active;
    public string Ownership { get; set; } = PropOwnershipTypes.Owned;

    /* ---------------- description ---------------- */

    /// <summary>Free text as written in the register — "6 FIT", "14'10'8'6 INCH".</summary>
    public string? Size { get; set; }

    public string? Colour { get; set; }

    /// <summary>Velvet, brass, fibre — what it is made of, where the register said.</summary>
    public string? Material { get; set; }

    /// <summary>PCS, SET, KG, THAAN. The unit the counts below are in.</summary>
    public string Unit { get; set; } = "PCS";

    public string? Description { get; set; }

    /// <summary>Comma-separated search aids — "mandap, entrance, haldi".</summary>
    public string? Tags { get; set; }

    /* ---------------- counts ---------------- */

    /// <summary>Usable pieces. The number that answers "can we do it?".</summary>
    public int GoodQuantity { get; set; }

    /// <summary>Broken but worth mending. Not offered to an event.</summary>
    public int RepairableQuantity { get; set; }

    /// <summary>Beyond repair, still on the books until written off.</summary>
    public int DamagedQuantity { get; set; }

    /// <summary>
    /// Below this, the catalogue flags the line for replenishment. Zero means
    /// the line is not watched.
    /// </summary>
    public int ReorderLevel { get; set; }

    /* ---------------- commercials ---------------- */

    /// <summary>What one piece is charged at, per event day.</summary>
    public decimal? RentalRatePerDay { get; set; }

    /// <summary>What it cost to buy. Drives depreciation and the damage bill.</summary>
    public decimal? PurchaseCost { get; set; }

    /// <summary>What the client is billed if a piece does not come back.</summary>
    public decimal? ReplacementValue { get; set; }

    /// <summary>Who we hire it from, when <see cref="Ownership"/> is SubHired.</summary>
    public string? SupplierName { get; set; }

    public DateOnly? PurchaseDate { get; set; }

    /* ---------------- handling ---------------- */

    /// <summary>Weight of one piece in kg, for load planning.</summary>
    public decimal? WeightKg { get; set; }

    /// <summary>Pieces that travel in one crate — the number the loaders count in.</summary>
    public int? PackingUnit { get; set; }

    /// <summary>Needs careful handling. Surfaces as a warning on the gate pass.</summary>
    public bool IsFragile { get; set; }

    /// <summary>Each piece carries its own tag or serial number.</summary>
    public bool IsSerialised { get; set; }

    /// <summary>
    /// Days needed between a return and the next dispatch — laundering a canopy,
    /// re-gilding a pot. Widens the window a reservation actually blocks.
    /// </summary>
    public int TurnaroundDays { get; set; }

    /// <summary>Where in the godown — rack, shelf, container.</summary>
    public string? StorageLocation { get; set; }

    public int? OwnerId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    public PropCategory? Category { get; set; }
    public PropStore? Store { get; set; }
    public User? Owner { get; set; }
    public ICollection<PropItemPhoto> Photos { get; set; } = new List<PropItemPhoto>();
    public ICollection<PropStockMovement> Movements { get; set; } = new List<PropStockMovement>();

    /// <summary>Every piece on the books, whatever condition it is in.</summary>
    public int OnHandQuantity => GoodQuantity + RepairableQuantity + DamagedQuantity;

    /// <summary>
    /// The ceiling on what can be promised to an event, before reservations.
    /// Repairable and damaged stock is on the books but cannot be sent out.
    /// </summary>
    public int UsableQuantity => GoodQuantity;

    /// <summary>Whether the line has fallen to or below its watch level.</summary>
    public bool IsBelowReorderLevel => ReorderLevel > 0 && GoodQuantity <= ReorderLevel;
}

/// <summary>
/// A photograph of an item. Several per line — the register carries one, but a
/// coordinator picking décor wants the piece from more than one angle.
/// </summary>
public class PropItemPhoto
{
    public int Id { get; set; }
    public int PropItemId { get; set; }

    /// <summary>Path under the API's static root — <c>/props/full/{file}</c>.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>The 320px version the catalogue grid loads.</summary>
    public string? ThumbnailUrl { get; set; }

    public string? Caption { get; set; }
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public PropItem? Item { get; set; }
}

/* ------------------------------------------------------------------ *
 * The ledger
 * ------------------------------------------------------------------ */

/// <summary>
/// One movement of stock, append-only.
///
/// The condition counts on <see cref="PropItem"/> are a running total of these
/// rows, maintained as they are written. Keeping the ledger means a disputed
/// count has an answer — who moved what, when, and against which event — which
/// a mutable quantity column can never give.
/// </summary>
public class PropStockMovement : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int PropItemId { get; set; }

    /// <summary>A value from <see cref="PropMovementTypes"/>.</summary>
    public string MovementType { get; set; } = PropMovementTypes.Adjustment;

    /// <summary>
    /// Pieces moved, signed the way the movement counts — negative for an issue,
    /// positive for a return. Written by the controller from
    /// <see cref="PropMovementTypes.Delta"/> so the sign can never contradict
    /// the verb.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>Which bucket the pieces left, for a reclassification.</summary>
    public string? FromCondition { get; set; }

    /// <summary>Which bucket they landed in.</summary>
    public string? ToCondition { get; set; }

    /// <summary>Good stock after this row was applied. The audit anchor.</summary>
    public int BalanceAfter { get; set; }

    public int? PropIssueId { get; set; }
    public int? StoreId { get; set; }

    /// <summary>The event this movement served, where there was one.</summary>
    public int? LeadId { get; set; }
    public int? BookingId { get; set; }

    /// <summary>Money attached — a purchase cost, a damage recovery.</summary>
    public decimal? Amount { get; set; }

    public string? Notes { get; set; }

    /// <summary>Who physically handled it. Free text: it is often a loader.</summary>
    public string? HandledBy { get; set; }

    public DateTime MovedAt { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public PropItem? Item { get; set; }
    public PropIssue? Issue { get; set; }
}

/* ------------------------------------------------------------------ *
 * Gate pass
 * ------------------------------------------------------------------ */

/// <summary>
/// A dispatch to one event — the gate pass the truck leaves with, and the
/// checklist it is counted back against.
///
/// Dates are a window rather than a day. A wedding takes props out on the 10th
/// for a setup on the 12th and returns them on the 15th, and every one of those
/// days is a day the stock cannot be at another event. Availability is checked
/// against the whole window, dispatch to return.
/// </summary>
public class PropIssue : ITenantScoped, ISoftDeletable, IAuditable, IOwnedRecord
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    /// <summary>Quotable reference — <c>GP-2609-K3P9QF</c>.</summary>
    public string Code { get; set; } = string.Empty;

    public string Status { get; set; } = PropIssueStatuses.Draft;

    /* ---------------- what it is for ---------------- */

    public int? LeadId { get; set; }
    public int? BookingId { get; set; }
    public int? QuotationId { get; set; }

    /// <summary>The venue, when the event is at one we hold on the books.</summary>
    public int? ProjectId { get; set; }

    /// <summary>Denormalised so the list reads without four joins per row.</summary>
    public string? EventName { get; set; }
    public string? EventType { get; set; }
    public string? ClientName { get; set; }
    public string? VenueName { get; set; }
    public string? VenueAddress { get; set; }

    /* ---------------- the window ---------------- */

    /// <summary>When the stock leaves the godown.</summary>
    public DateOnly DispatchDate { get; set; }

    /// <summary>The day of the function itself. Sits inside the window.</summary>
    public DateOnly? EventDate { get; set; }

    /// <summary>When it is due back. The date the return chaser runs off.</summary>
    public DateOnly ExpectedReturnDate { get; set; }

    /// <summary>When it actually came back.</summary>
    public DateOnly? ActualReturnDate { get; set; }

    /* ---------------- logistics ---------------- */

    public int? StoreId { get; set; }

    /// <summary>Vehicle number on the pass, for the gate register.</summary>
    public string? VehicleNumber { get; set; }

    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }

    /// <summary>Crew member accountable for the load on site.</summary>
    public int? SiteInChargeId { get; set; }

    public string? Notes { get; set; }

    /// <summary>What the shortages and breakages came to, once reconciled.</summary>
    public decimal? DamageRecovery { get; set; }

    public int? OwnerId { get; set; }

    public DateTime? DispatchedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    public Lead? Lead { get; set; }
    public Project? Project { get; set; }
    public PropStore? Store { get; set; }
    public User? SiteInCharge { get; set; }
    public ICollection<PropIssueLine> Lines { get; set; } = new List<PropIssueLine>();

    /// <summary>Whether this pass still holds its stock away from other events.</summary>
    public bool IsBlocking => PropIssueStatuses.Blocking.Contains(Status);

    /// <summary>
    /// Whether the pass overlaps a window, and so competes for the same stock.
    ///
    /// Inclusive at both ends: a pass due back on the 14th and one dispatching
    /// on the 14th want the same crates on the same day, and treating that as
    /// free is how a load goes out short.
    /// </summary>
    public bool Overlaps(DateOnly from, DateOnly to) =>
        DispatchDate <= to && ExpectedReturnDate >= from;
}

/// <summary>
/// One item on a gate pass, with the three counts that a dispute turns on:
/// what was promised, what physically left, and what came back.
///
/// They are separate columns because they genuinely differ — twenty vases are
/// reserved, eighteen fit on the truck, sixteen come home and two are billed.
/// Collapsing them into one quantity loses exactly the information the
/// reconciliation needs.
/// </summary>
public class PropIssueLine
{
    public int Id { get; set; }
    public int PropIssueId { get; set; }
    public int PropItemId { get; set; }

    /// <summary>Committed at reservation time. Blocks stock for the window.</summary>
    public int ReservedQuantity { get; set; }

    /// <summary>Counted onto the vehicle.</summary>
    public int IssuedQuantity { get; set; }

    /// <summary>Counted back in, in usable condition.</summary>
    public int ReturnedQuantity { get; set; }

    /// <summary>Came back broken.</summary>
    public int DamagedQuantity { get; set; }

    /// <summary>Did not come back at all.</summary>
    public int LostQuantity { get; set; }

    /// <summary>Legitimately used up — petals, candles. Never chased.</summary>
    public int ConsumedQuantity { get; set; }

    /// <summary>Charged to the client for this line, per piece per day.</summary>
    public decimal? RatePerDay { get; set; }

    /// <summary>Days billed. Usually the window, but negotiable.</summary>
    public int ChargeableDays { get; set; } = 1;

    public string? Notes { get; set; }
    public int SortOrder { get; set; }

    public PropIssue? Issue { get; set; }
    public PropItem? Item { get; set; }

    /// <summary>
    /// Pieces still outstanding against this line.
    ///
    /// Damaged and lost pieces count as settled: they are not coming back, and
    /// leaving them in the outstanding figure would keep a closed pass looking
    /// unreturned forever.
    /// </summary>
    public int PendingQuantity => Math.Max(
        0,
        IssuedQuantity - ReturnedQuantity - DamagedQuantity - LostQuantity - ConsumedQuantity);

    /// <summary>What this line adds to the quote.</summary>
    public decimal LineTotal => (RatePerDay ?? 0) * Math.Max(ReservedQuantity, IssuedQuantity)
        * Math.Max(1, ChargeableDays);
}

/* ------------------------------------------------------------------ *
 * Kits
 * ------------------------------------------------------------------ */

/// <summary>
/// A set that always travels together — a mandap, an entrance arch, a haldi
/// stage, a round-table centrepiece.
///
/// The venue side already has <see cref="VenuePackage"/> for the same reason,
/// and the reason is the same here: a mandap is forty items, and adding them to
/// a gate pass one line at a time is precisely where a load goes out
/// incomplete. Expanding a kit writes the forty lines in one action, and each
/// still gets its own availability check — a kit that cannot be fully supplied
/// has to say so rather than ship short.
/// </summary>
public class PropKit : ITenantScoped, ISoftDeletable, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Quotable reference — <c>KIT-MANDAP-01</c>.</summary>
    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>What kind of setup it is — Mandap, Entrance, Stage, TableSet.</summary>
    public string? SetupType { get; set; }

    /// <summary>The occasion it suits, from <see cref="EventTypes"/>. Null suits any.</summary>
    public string? EventType { get; set; }

    /// <summary>Photograph of the assembled set, which is what a client picks from.</summary>
    public string? CoverImageUrl { get; set; }

    /// <summary>
    /// What the whole kit is quoted at per day, where it is priced as a set.
    /// Null falls back to summing its lines' own rates.
    /// </summary>
    public decimal? RentalRatePerDay { get; set; }

    /// <summary>Hours to build it on site. Drives how early the truck has to leave.</summary>
    public decimal? SetupHours { get; set; }

    /// <summary>People needed to build it. Feeds the crew requisition.</summary>
    public int? CrewRequired { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    public ICollection<PropKitLine> Lines { get; set; } = new List<PropKitLine>();
}

/// <summary>One item, and how many of it, in a kit.</summary>
public class PropKitLine
{
    public int Id { get; set; }
    public int PropKitId { get; set; }
    public int PropItemId { get; set; }

    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Whether the kit can go out without this line.
    ///
    /// A mandap without its pillars is not a mandap; a mandap without the
    /// decorative urlis is a slightly plainer mandap. Marking the difference is
    /// what lets the expansion warn about a shortfall instead of refusing it.
    /// </summary>
    public bool IsOptional { get; set; }

    public string? Notes { get; set; }
    public int SortOrder { get; set; }

    public PropKit? Kit { get; set; }
    public PropItem? Item { get; set; }
}

/* ------------------------------------------------------------------ *
 * Reservations
 * ------------------------------------------------------------------ */

/// <summary>
/// A hold on a quantity of one item across a date window.
///
/// This is the prop-side answer to <c>SpaceBooking</c>, and it exists for the
/// same reason: the only question that matters — "can I promise forty gold
/// vases for the 14th?" — is a query across overlapping windows, not a status
/// on the item.
///
/// A row is written for every line of a committed gate pass, and can also stand
/// alone: a quotation being negotiated holds stock softly, with
/// <see cref="ExpiresAt"/> set, so a deal that goes quiet releases the crates
/// on its own instead of freezing them until somebody remembers.
/// </summary>
public class PropReservation : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int PropItemId { get; set; }

    /// <summary>Set once the reservation belongs to a gate pass.</summary>
    public int? PropIssueId { get; set; }

    public int? LeadId { get; set; }
    public int? QuotationId { get; set; }
    public int? BookingId { get; set; }

    public int Quantity { get; set; }

    /// <summary>First day the stock is unavailable — the dispatch, not the event.</summary>
    public DateOnly FromDate { get; set; }

    /// <summary>Last day it is unavailable — the return, plus any turnaround.</summary>
    public DateOnly ToDate { get; set; }

    /// <summary>Reserved, or Released once it no longer counts.</summary>
    public string Status { get; set; } = PropIssueStatuses.Reserved;

    /// <summary>
    /// When a soft hold stops blocking. Null for a hold tied to a confirmed
    /// gate pass, which is released explicitly rather than by lapsing.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Denormalised for the availability calendar's tooltips.</summary>
    public string? EventName { get; set; }
    public string? ClientName { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public PropItem? Item { get; set; }
    public PropIssue? Issue { get; set; }

    /// <summary>Whether this hold still counts against stock, as of <paramref name="now"/>.</summary>
    public bool IsBlocking(DateTime now) =>
        Status == PropIssueStatuses.Reserved && (ExpiresAt is null || ExpiresAt > now);

    /// <summary>Whether the hold's window touches the one being asked about.</summary>
    public bool Overlaps(DateOnly from, DateOnly to) => FromDate <= to && ToDate >= from;
}
