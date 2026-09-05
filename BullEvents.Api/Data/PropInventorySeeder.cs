using System.Text.Json;
using System.Text.Json.Serialization;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Data;

/// <summary>
/// Loads the décor godown register — 734 lines and 512 photographs — out of
/// <c>Data/Seed/decor-props.json</c>, which is the workbook the team actually
/// keeps, flattened.
///
/// This is real stock, not a demonstration set, so it is guarded harder than
/// the demo seeders: it does nothing at all once a single prop item exists on
/// the tenant. Re-running it can never duplicate the register, and a company
/// that has started editing its own catalogue is never touched.
///
/// The photographs are already on disk under <c>wwwroot/props</c>, resized to a
/// 900px display copy and a 320px thumbnail. Only the paths are stored.
/// </summary>
public static class PropInventorySeeder
{
    private sealed record PropRow(
        [property: JsonPropertyName("category")] string Category,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("material")] string? Material,
        [property: JsonPropertyName("size")] string? Size,
        [property: JsonPropertyName("unit")] string Unit,
        [property: JsonPropertyName("colour")] string? Colour,
        [property: JsonPropertyName("good")] int Good,
        [property: JsonPropertyName("repairable")] int Repairable,
        [property: JsonPropertyName("damaged")] int Damaged,
        [property: JsonPropertyName("total")] int Total,
        [property: JsonPropertyName("images")] List<string> Images);

    /// <summary>
    /// The workbook's sheet names, mapped to a stock-code prefix and the kind
    /// of thing the sheet holds.
    ///
    /// Written out by hand rather than derived: "CLOTHES" is fabric, "FLOWERS"
    /// is floral and "TABLE TOP" is décor rather than furniture, and no
    /// slugging rule gets all three right.
    /// </summary>
    private static readonly Dictionary<string, (string Code, string Type)> Sections =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["MIXED PROPS"] = ("MIXED", PropItemTypes.Prop),
            ["CANDLES, BULBS & CANDLE STAND"] = ("CANDLE", PropItemTypes.Prop),
            ["RUGS & HANGINGS"] = ("RUG", PropItemTypes.Fabric),
            ["BRASS ITEMS"] = ("BRASS", PropItemTypes.Prop),
            ["CERAMIC POTS"] = ("CERAMIC", PropItemTypes.Prop),
            ["FIBER ITEMS"] = ("FIBRE", PropItemTypes.Prop),
            ["WOODEN & ANTIQUE PROPS"] = ("WOOD", PropItemTypes.Prop),
            ["CENTER INSTALLATION"] = ("INSTALL", PropItemTypes.Structure),
            ["GLASS PROPS & MIRROR"] = ("GLASS", PropItemTypes.Prop),
            ["MOROCCAN PROPS"] = ("MORO", PropItemTypes.Prop),
            ["CHANDELIERS"] = ("CHAND", PropItemTypes.Prop),
            ["Table"] = ("TABLE", PropItemTypes.Furniture),
            ["Furniture Sofa & Chairs"] = ("FURN", PropItemTypes.Furniture),
            ["TABLE TOP"] = ("TBLTOP", PropItemTypes.Prop),
            ["CANOPY"] = ("CANOPY", PropItemTypes.Fabric),
            ["CHAIR COVER & GADI COVER"] = ("COVER", PropItemTypes.Fabric),
            ["CONSOLE TABLE"] = ("CONSOLE", PropItemTypes.Furniture),
            ["NAME LATTERS"] = ("LETTER", PropItemTypes.Prop),
            ["CUTOUTS"] = ("CUTOUT", PropItemTypes.Prop),
            ["CLOTHES"] = ("CLOTH", PropItemTypes.Fabric),
            ["FLOWERS"] = ("FLOWER", PropItemTypes.Floral),
        };

    /// <summary>
    /// Sheet names in shouting caps become something a person can read on a
    /// screen — "GLASS PROPS &amp; MIRROR" to "Glass Props &amp; Mirror".
    /// </summary>
    private static string TitleCase(string value) =>
        string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Length <= 1
                ? word.ToUpperInvariant()
                : char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));

    public static async Task SeedAsync(AppDbContext db, IWebHostEnvironment environment)
    {
        // One prop line anywhere means this catalogue is in use. Never add to it.
        if (await db.PropItems.IgnoreQueryFilters().AnyAsync()) return;

        var company = await db.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync();
        if (company is null) return;

        var path = Path.Combine(environment.ContentRootPath, "Data", "Seed", "decor-props.json");
        if (!File.Exists(path)) return;

        var rows = JsonSerializer.Deserialize<List<PropRow>>(await File.ReadAllTextAsync(path));
        if (rows is null or { Count: 0 }) return;

        var owner = await db.Users
            .Where(u => u.CompanyId == company.Id && u.Role != Roles.SuperAdmin)
            .OrderBy(u => u.Id)
            .FirstOrDefaultAsync();

        /* ---------------- the godown ---------------- */

        var store = new PropStore
        {
            CompanyId = company.Id,
            Name = "Main Godown",
            Code = "GODOWN",
            KeeperId = owner?.Id,
        };

        db.PropStores.Add(store);

        /* ---------------- sections ---------------- */

        // Ordered by first appearance in the workbook, so the catalogue's
        // sections read in the order the team already knows them in.
        var sectionNames = rows.Select(r => r.Category).Distinct().ToList();
        var categories = new Dictionary<string, PropCategory>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, index) in sectionNames.Select((n, i) => (n, i)))
        {
            var known = Sections.TryGetValue(name, out var section)
                ? section
                : (Code: new string(name.Where(char.IsLetter).Take(6).ToArray()).ToUpperInvariant(),
                   Type: PropItemTypes.Prop);

            var category = new PropCategory
            {
                CompanyId = company.Id,
                Name = TitleCase(name),
                Code = known.Code,
                DefaultItemType = known.Type,
                SortOrder = index,
            };

            categories[name] = category;
            db.PropCategories.Add(category);
        }

        await db.SaveChangesAsync();

        /* ---------------- the register ---------------- */

        var sequence = sectionNames.ToDictionary(n => n, _ => 0, StringComparer.OrdinalIgnoreCase);
        var items = new List<PropItem>(rows.Count);

        foreach (var row in rows)
        {
            var category = categories[row.Category];
            var next = ++sequence[row.Category];

            // The workbook's TOTAL (USABLE) column is the figure the team
            // trusts; the GOOD column is blank on a good many rows. Where both
            // are present they agree, so preferring the total is safe and it
            // stops several hundred lines importing as zero stock.
            var good = Math.Max(row.Total, row.Good);

            var item = new PropItem
            {
                CompanyId = company.Id,
                CategoryId = category.Id,
                StoreId = store.Id,
                Name = TitleCase(row.Name),
                Code = $"{category.Code}-{next:D4}",
                ItemType = category.DefaultItemType,
                Status = PropItemStatuses.Active,
                Ownership = PropOwnershipTypes.Owned,
                Size = row.Size,
                Colour = row.Colour is null ? null : TitleCase(row.Colour),
                Material = row.Material is null ? null : TitleCase(row.Material),
                Unit = row.Unit,
                GoodQuantity = good,
                RepairableQuantity = row.Repairable,
                DamagedQuantity = row.Damaged,
                OwnerId = owner?.Id,
            };

            foreach (var (image, order) in row.Images.Select((f, i) => (f, i)))
            {
                item.Photos.Add(new PropItemPhoto
                {
                    Url = $"/props/full/{image}",
                    ThumbnailUrl = $"/props/thumb/{image}",
                    SortOrder = order,
                });
            }

            items.Add(item);
        }

        db.PropItems.AddRange(items);
        await db.SaveChangesAsync();

        /* ---------------- opening balances ---------------- */

        // Every line starts its stock card with a stated opening figure, so the
        // ledger reconciles to the counts from the very first day rather than
        // from the first movement somebody happens to record.
        db.PropStockMovements.AddRange(items
            .Where(i => i.GoodQuantity > 0)
            .Select(i => new PropStockMovement
            {
                CompanyId = company.Id,
                PropItemId = i.Id,
                MovementType = PropMovementTypes.Opening,
                Quantity = i.GoodQuantity,
                ToCondition = PropConditions.Good,
                BalanceAfter = i.GoodQuantity,
                StoreId = store.Id,
                Notes = "Imported from the décor props register",
            }));

        await db.SaveChangesAsync();
    }
}
