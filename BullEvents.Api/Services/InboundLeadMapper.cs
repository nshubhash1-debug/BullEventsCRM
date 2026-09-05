using System.Text.Json;
using System.Text.Json.Nodes;
using BullEvents.Api.Models;

namespace BullEvents.Api.Services;

/// <summary>What one delivery turned out to contain.</summary>
public record MappedLead(
    string? Name,
    string? Phone,
    string? Email,
    string? City,
    string? Locality,
    /// <summary>The occasion, as the form worded it.</summary>
    string? Requirement,
    string? Message,
    string? Campaign,
    string? ExternalId,
    decimal? BudgetMin,
    decimal? BudgetMax,
    /// <summary>The date of the event, when the form asked for one.</summary>
    DateTime? EventDate,
    /// <summary>Expected footfall, when the form asked for it.</summary>
    int? GuestCount,
    /// <summary>Anything left over, kept verbatim on the lead's notes.</summary>
    IReadOnlyDictionary<string, string> Extras);

/// <summary>
/// Turns whatever a provider posted into the fields of a lead.
///
/// Every portal, ad platform and website form invents its own field names, and
/// several change them without telling anybody. Rather than a mapping table per
/// provider that goes stale silently, this matches on a list of candidate names
/// per field, case- and separator-insensitive, and keeps everything it did not
/// recognise instead of dropping it. A feed that renames "mobile" to
/// "contact_number" keeps working; one that invents something genuinely new
/// still lands, with the unknown value visible on the lead.
///
/// Two shapes get special handling because they are not flat: Meta's lead-ad
/// payload nests the answers under <c>field_data</c>, and several portals wrap
/// theirs in a single-item array or a <c>data</c> envelope.
/// </summary>
public static class InboundLeadMapper
{
    /// <summary>
    /// Candidate keys per field, best first.
    ///
    /// Compared after stripping everything but letters and digits, so
    /// <c>full_name</c>, <c>fullName</c> and <c>Full Name</c> are one key.
    /// </summary>
    private static readonly (string Field, string[] Keys)[] Aliases =
    [
        ("name", ["name", "fullname", "customername", "leadname", "firstname", "contactname", "username", "clientname"]),
        ("phone", ["phone", "mobile", "mobileno", "phonenumber", "contactnumber", "contactno", "cell", "whatsapp", "callerid", "from"]),
        ("email", ["email", "emailaddress", "mail", "emailid"]),
        ("city", ["city", "location", "town"]),
        ("locality", ["locality", "area", "preferredlocality", "neighbourhood", "sublocality"]),
        ("requirement", ["eventtype", "occasion", "event", "functiontype", "requirement", "interestedin", "servicetype", "package"]),
        ("eventdate", ["eventdate", "date", "functiondate", "weddingdate", "preferreddate", "dateofevent", "eventon"]),
        ("guestcount", ["guestcount", "guests", "noofguests", "numberofguests", "pax", "footfall", "headcount", "attendees", "capacity"]),
        ("message", ["message", "comments", "query", "remarks", "note", "notes", "description", "enquiry"]),
        ("campaign", ["campaign", "campaignname", "adname", "adsetname", "utmcampaign", "source", "formname"]),
        ("externalid", ["leadid", "id", "enquiryid", "externalid", "referenceid", "leadgenid", "requestid"]),
        ("budgetmin", ["budgetmin", "minbudget", "budgetfrom", "pricemin"]),
        ("budgetmax", ["budgetmax", "maxbudget", "budgetto", "pricemax", "budget"]),
        ("project", ["venue", "venuename", "project", "projectname", "property", "propertyname", "listing", "listingname"]),
    ];

    public static MappedLead Map(JsonNode? payload)
    {
        var flat = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Flatten(Unwrap(payload), flat, depth: 0);

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string? Pick(string field)
        {
            var keys = Aliases.First(a => a.Field == field).Keys;

            foreach (var candidate in keys)
            {
                var match = flat.Keys.FirstOrDefault(
                    k => Normalise(k) == candidate && !string.IsNullOrWhiteSpace(flat[k]));

                if (match is not null)
                {
                    used.Add(match);
                    return flat[match].Trim();
                }
            }

            return null;
        }

        // Every field is picked before the leftovers are worked out. Picking some
        // of them inside the constructor below would compute `extras` first, and
        // the note would repeat the phone number and the email that had already
        // been mapped onto their own columns.
        var name = Pick("name");
        var project = Pick("project");
        var message = Pick("message");
        var phone = Digits(Pick("phone"));
        var email = Pick("email");
        var city = Pick("city");
        var locality = Pick("locality");
        var requirement = Pick("requirement");
        var campaign = Pick("campaign");
        var externalId = Pick("externalid");
        var budgetMin = Money(Pick("budgetmin"));
        var budgetMax = Money(Pick("budgetmax"));
        var eventDate = Date(Pick("eventdate"));
        var guestCount = Count(Pick("guestcount"));

        // A venue name is worth keeping even though there is no column for it
        // on the lead: it is usually the single most useful thing in a portal
        // enquiry, and losing it makes the lead unactionable.
        if (project is not null)
        {
            message = string.IsNullOrWhiteSpace(message)
                ? $"Enquired about {project}."
                : $"Enquired about {project}. {message}";
        }

        var extras = flat
            .Where(pair => !used.Contains(pair.Key))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Where(pair => !Noise.Contains(Normalise(pair.Key)))
            .Take(20)
            .ToDictionary(pair => pair.Key, pair => Trim(pair.Value, 200));

        return new MappedLead(
            Name: name,
            Phone: phone,
            Email: email,
            City: city,
            Locality: locality,
            Requirement: requirement,
            Message: message,
            Campaign: campaign,
            ExternalId: externalId,
            BudgetMin: budgetMin,
            BudgetMax: budgetMax,
            EventDate: eventDate,
            GuestCount: guestCount,
            Extras: extras);
    }

    /// <summary>
    /// Peels the envelopes providers wrap a single enquiry in — a one-item
    /// array, a <c>data</c> or <c>lead</c> object, Meta's <c>entry/changes</c>
    /// chain — so the flattener sees the enquiry rather than the packaging.
    /// </summary>
    private static JsonNode? Unwrap(JsonNode? node)
    {
        for (var guard = 0; guard < 8 && node is not null; guard++)
        {
            if (node is JsonArray array)
            {
                if (array.Count != 1) return node;
                node = array[0];
                continue;
            }

            if (node is not JsonObject obj) return node;

            var envelope = new[] { "data", "lead", "leads", "payload", "body", "entry", "changes", "value" }
                .FirstOrDefault(key => obj.ContainsKey(key)
                                       && obj[key] is JsonObject or JsonArray);

            if (envelope is null) return node;

            // Only unwrap when the envelope is the whole message. An object that
            // carries `data` *and* a name at the top level is a flat payload
            // that happens to have a nested extra, and peeling it would throw
            // the name away.
            var siblings = obj.Count(pair => pair.Key != envelope
                                             && pair.Value is JsonValue);

            if (siblings > 1) return node;

            node = obj[envelope];
        }

        return node;
    }

    /// <summary>
    /// Walks the payload into flat key/value pairs.
    ///
    /// Meta's <c>field_data</c> is the one shape that cannot be flattened
    /// generically — it is a list of <c>{ name, values }</c> objects, so the
    /// answer's key is a *value* rather than a property name.
    /// </summary>
    private static void Flatten(JsonNode? node, Dictionary<string, string> into, int depth)
    {
        if (node is null || depth > 6 || into.Count > 200) return;

        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj)
                {
                    if (string.Equals(key, "field_data", StringComparison.OrdinalIgnoreCase)
                        && value is JsonArray fields)
                    {
                        ReadMetaFields(fields, into);
                        continue;
                    }

                    if (value is JsonValue)
                    {
                        // First writer wins: an outer, more specific key should
                        // not be overwritten by a generic one further down.
                        if (!into.ContainsKey(key)) into[key] = value.ToString();
                    }
                    else
                    {
                        Flatten(value, into, depth + 1);
                    }
                }

                break;

            case JsonArray array:
                foreach (var child in array) Flatten(child, into, depth + 1);
                break;
        }
    }

    private static void ReadMetaFields(JsonArray fields, Dictionary<string, string> into)
    {
        foreach (var field in fields.OfType<JsonObject>())
        {
            var key = field["name"]?.ToString();
            if (string.IsNullOrWhiteSpace(key)) continue;

            var value = field["values"] switch
            {
                JsonArray values => string.Join(", ", values.Select(v => v?.ToString()).Where(v => v is not null)),
                JsonNode single => single.ToString(),
                _ => null,
            };

            if (!string.IsNullOrWhiteSpace(value) && !into.ContainsKey(key)) into[key] = value;
        }
    }

    /// <summary>Keys that are plumbing rather than content, so they stay out of the notes.</summary>
    private static readonly HashSet<string> Noise = new(StringComparer.OrdinalIgnoreCase)
    {
        "object", "time", "createdtime", "signature", "token", "verifytoken", "secret",
        "adid", "adgroupid", "formid", "pageid", "platform", "ischeckbox", "type",
    };

    private static string Normalise(string key) =>
        new(key.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    /// <summary>
    /// Keeps the digits and a leading +.
    ///
    /// Portals send "+91 98200-11122", "098200 11122" and "9820011122" for one
    /// number, and duplicate detection compares them as strings.
    /// </summary>
    private static string? Digits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim();
        var plus = trimmed.StartsWith('+');
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());

        if (digits.Length < 6) return null;

        return plus ? "+" + digits : digits;
    }

    /// <summary>
    /// Reads a budget written the way Indian portals write one — "45 lakh",
    /// "1.2 Cr", "₹85,00,000" — which no number parser handles on its own.
    /// </summary>
    /// <summary>
    /// Reads whatever a form called a date.
    ///
    /// Day-first is tried before the invariant parse because every Indian form
    /// writes 04/11/2026 meaning the fourth of November, and letting
    /// <c>DateTime.Parse</c> read it as the eleventh of April would silently
    /// put the wedding seven months early.
    /// </summary>
    private static DateTime? Date(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var text = value.Trim();

        string[] dayFirst =
        [
            "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy",
            "yyyy-MM-dd", "dd MMM yyyy", "d MMM yyyy", "dd MMMM yyyy", "d MMMM yyyy",
        ];

        if (DateTime.TryParseExact(
                text, dayFirst,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var exact))
        {
            return DateTime.SpecifyKind(exact.Date, DateTimeKind.Utc);
        }

        return DateTime.TryParse(
            text, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var parsed)
            ? DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc)
            : null;
    }

    /// <summary>
    /// Reads a guest count out of whatever the form collected — "500",
    /// "500-600", "around 500 pax". Takes the first number it finds, which for
    /// a range is the lower bound, and refuses anything implausible so a phone
    /// number landing in the wrong field cannot become a 98-lakh-guest wedding.
    /// </summary>
    private static int? Count(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var digits = new string(value.TakeWhile(c => !char.IsDigit(c))
            .Concat(value.SkipWhile(c => !char.IsDigit(c)).TakeWhile(char.IsDigit))
            .Where(char.IsDigit).ToArray());

        if (digits.Length == 0 || !int.TryParse(digits, out var count)) return null;

        return count is > 0 and <= 100_000 ? count : null;
    }

    private static decimal? Money(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var text = value.Trim().ToLowerInvariant();

        var multiplier = 1m;
        if (text.Contains("crore") || text.Contains("cr")) multiplier = 10_000_000m;
        else if (text.Contains("lakh") || text.Contains("lac") || text.Contains("l ")) multiplier = 100_000m;

        var digits = new string(text.Where(c => char.IsDigit(c) || c == '.').ToArray());
        if (digits.Length == 0) return null;

        if (!decimal.TryParse(digits, out var number)) return null;

        var amount = number * multiplier;

        // A figure that came through as a bare number with no unit is already in
        // rupees if it is large enough to be a property price.
        return amount > 0 && amount < 1_000_000_000_000m ? amount : null;
    }

    private static string Trim(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";

    /// <summary>The leftover fields, written the way a person reads a note.</summary>
    public static string? ExtrasNote(IReadOnlyDictionary<string, string> extras)
    {
        if (extras.Count == 0) return null;

        return string.Join("\n", extras.Select(pair => $"{pair.Key}: {pair.Value}"));
    }

    /// <summary>Parses a request body into a node, tolerating an empty or malformed one.</summary>
    public static JsonNode? Parse(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        try
        {
            return JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
