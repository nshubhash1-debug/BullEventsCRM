namespace BullEvents.Api.Models;

/// <summary>
/// One field a company can take away without taking away the record.
///
/// The name is the JSON property the API sends, because that is what the
/// stripping filter matches on and what an administrator sees in the browser's
/// network tab if they go looking. <see cref="Sensitive"/> only marks which
/// ones lead the list — the field is securable either way.
/// </summary>
public record SecurableField(string Object, string Name, string Label, string Why, bool Sensitive = false);

/// <summary>
/// The fields the console can restrict.
///
/// A curated list rather than reflection over the DTOs, for the same reason
/// <see cref="SecuredObjects"/> is: a stored permission should not change
/// meaning because somebody renamed a property. Restricting everything would
/// also be a worse product — an administrator facing ninety checkboxes per
/// object stops reading them.
/// </summary>
public static class SecurableFields
{
    public static readonly SecurableField[] All =
    [
        /* ---------------- lead ---------------- */
        new(SecuredObjects.Lead, "phone", "Phone", "The number itself, on the lead record and in exports", Sensitive: true),
        new(SecuredObjects.Lead, "phone2", "Alternate phone", "The second contact number", Sensitive: true),
        new(SecuredObjects.Lead, "email", "Email", "The lead's email address", Sensitive: true),
        new(SecuredObjects.Lead, "address", "Address", "Street address of the lead"),
        new(SecuredObjects.Lead, "dateOfBirth", "Date of birth", "Personal data that most seats have no use for", Sensitive: true),
        new(SecuredObjects.Lead, "budgetMin", "Budget from", "What the buyer said they would spend"),
        new(SecuredObjects.Lead, "budgetMax", "Budget to", "What the buyer said they would spend"),
        new(SecuredObjects.Lead, "score", "Lead score", "The model's number, which reps sometimes work rather than the lead"),
        new(SecuredObjects.Lead, "band", "Lead band", "Hot, warm, cool or cold, derived from the score"),
        new(SecuredObjects.Lead, "notes", "Notes", "Free text, which is where sensitive detail usually ends up"),
        new(SecuredObjects.Lead, "referredBy", "Referred by", "Who introduced the lead, and therefore who is owed for it"),

        /* ---------------- contact ---------------- */
        new(SecuredObjects.Contact, "phone", "Phone", "The number itself", Sensitive: true),
        new(SecuredObjects.Contact, "email", "Email", "The contact's email address", Sensitive: true),
        new(SecuredObjects.Contact, "address", "Address", "Street address of the contact"),

        /* ---------------- opportunity ---------------- */
        new(SecuredObjects.Opportunity, "value", "Deal value", "What the deal is worth"),
        new(SecuredObjects.Opportunity, "probability", "Probability", "The forecast weighting on the deal"),

        /* ---------------- quotation ---------------- */
        new(SecuredObjects.Quotation, "discountAmount", "Discount", "How far the price was moved, which is a negotiating position"),
        new(SecuredObjects.Quotation, "discountPercent", "Discount %", "The same number as a percentage"),
        new(SecuredObjects.Quotation, "netPayable", "Net payable", "The final figure after every adjustment"),

        /* ---------------- unit ---------------- */
        new(SecuredObjects.Unit, "basePrice", "Base price", "The list rate before any negotiation"),
        new(SecuredObjects.Unit, "offerPrice", "Offer price", "What this unit is currently being offered at"),

        /* ---------------- user ---------------- */
        new(SecuredObjects.User, "email", "Work email", "A colleague's address, which not every seat needs"),
        new(SecuredObjects.User, "lastLoginAt", "Last sign-in", "When somebody last used the CRM"),
    ];

    private static readonly Dictionary<string, SecurableField[]> ByObject =
        All.GroupBy(f => f.Object, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);

    public static SecurableField[] For(string securedObject) =>
        ByObject.TryGetValue(securedObject, out var fields) ? fields : [];

    public static bool Exists(string securedObject, string field) =>
        For(securedObject).Any(f => string.Equals(f.Name, field, StringComparison.OrdinalIgnoreCase));
}
