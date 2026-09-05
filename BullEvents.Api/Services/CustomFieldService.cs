using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>
/// Validates and normalises the values a company's own fields hold.
///
/// The definitions are the contract: anything the client sends under a key
/// nobody defined is dropped rather than stored, and anything that does not fit
/// its declared type is refused with a message naming the field. Storing
/// whatever arrives would turn the jsonb column into a place where typos live
/// forever, and the grid would be full of fields that exist on one record.
/// </summary>
public class CustomFieldService(AppDbContext db)
{
    /// <summary>The active definitions for one object, in the order they are shown.</summary>
    public Task<List<CustomFieldDefinition>> DefinitionsAsync(
        string securedObject, bool includeRetired = false, CancellationToken ct = default) =>
        db.CustomFieldDefinitions
            .Where(f => f.Object == securedObject && (includeRetired || f.IsActive))
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Id)
            .ToListAsync(ct);

    /// <summary>
    /// Folds an incoming set of values into the JSON to store.
    ///
    /// <paramref name="existing"/> is merged rather than replaced, so a client
    /// that sends three of eight fields does not blank the other five — the
    /// same reason a PATCH is not a PUT.
    /// </summary>
    public async Task<string?> NormaliseAsync(
        string securedObject,
        IReadOnlyDictionary<string, JsonElement>? incoming,
        string? existing,
        CancellationToken ct = default)
    {
        var definitions = await DefinitionsAsync(securedObject, ct: ct);

        // Nothing defined and nothing sent: leave whatever is already there
        // rather than writing an empty object over it.
        if (definitions.Count == 0) return existing;

        var stored = Parse(existing);

        if (incoming is not null)
        {
            foreach (var definition in definitions)
            {
                if (!incoming.TryGetValue(definition.Key, out var raw)) continue;

                var value = Coerce(definition, raw);

                if (value is null) stored.Remove(definition.Key);
                else stored[definition.Key] = value;
            }
        }

        foreach (var definition in definitions.Where(d => d.Required))
        {
            if (!stored.ContainsKey(definition.Key))
            {
                throw ApiException.BadRequest($"{definition.Label} is required.");
            }
        }

        return stored.Count == 0 ? null : JsonSerializer.Serialize(stored);
    }

    /// <summary>
    /// The stored values as a dictionary the DTO can carry, with anything whose
    /// definition has since been deleted dropped on the way out.
    /// </summary>
    public static Dictionary<string, JsonNode?> Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];

        try
        {
            return JsonNode.Parse(json) is JsonObject obj
                ? obj.ToDictionary(p => p.Key, p => p.Value?.DeepClone())
                : [];
        }
        catch (JsonException)
        {
            // A malformed bag is a bug somewhere upstream, but it must not stop
            // the record from being read.
            return [];
        }
    }

    private static Dictionary<string, JsonNode?> Parse(string? json) => Read(json);

    /// <summary>
    /// Turns one incoming value into what the field's type says it should be,
    /// or refuses it by name. Null means "clear this field".
    /// </summary>
    private static JsonNode? Coerce(CustomFieldDefinition definition, JsonElement raw)
    {
        if (raw.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;

        switch (definition.Type)
        {
            case CustomFieldType.Checkbox:
                return raw.ValueKind switch
                {
                    JsonValueKind.True => JsonValue.Create(true),
                    JsonValueKind.False => JsonValue.Create(false),
                    _ => throw Bad(definition, "must be true or false"),
                };

            case CustomFieldType.Number:
            {
                if (raw.ValueKind == JsonValueKind.Number) return JsonValue.Create(raw.GetDecimal());

                var text = raw.ToString();
                if (string.IsNullOrWhiteSpace(text)) return null;

                return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var number)
                    ? JsonValue.Create(number)
                    : throw Bad(definition, "must be a number");
            }

            case CustomFieldType.Date:
            {
                var text = raw.ToString();
                if (string.IsNullOrWhiteSpace(text)) return null;

                return DateTime.TryParse(
                        text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var date)
                    // Stored as a date rather than an instant: a custom "handover
                    // date" is a calendar day, and keeping a time on it means it
                    // shifts by a day for anybody in a different timezone.
                    ? JsonValue.Create(date.ToString("yyyy-MM-dd"))
                    : throw Bad(definition, "must be a date");
            }

            case CustomFieldType.Select:
            {
                var text = raw.ToString();
                if (string.IsNullOrWhiteSpace(text)) return null;

                var match = definition.Options.FirstOrDefault(
                    o => string.Equals(o, text, StringComparison.OrdinalIgnoreCase));

                return match is not null
                    ? JsonValue.Create(match)
                    : throw Bad(definition, $"must be one of: {string.Join(", ", definition.Options)}");
            }

            default:
            {
                var text = raw.ToString();
                if (string.IsNullOrWhiteSpace(text)) return null;

                var limit = definition.Type == CustomFieldType.LongText ? 4_000 : 400;

                return text.Length <= limit
                    ? JsonValue.Create(text)
                    : throw Bad(definition, $"must be {limit} characters or fewer");
            }
        }
    }

    private static ApiException Bad(CustomFieldDefinition definition, string what) =>
        ApiException.BadRequest($"{definition.Label} {what}.");

    /// <summary>
    /// Turns a label into the key it will be stored under, once.
    ///
    /// Lowercase, alphanumeric and dashes: it ends up as a JSON property name
    /// and as a column header, and both are better off without spaces.
    /// </summary>
    public static string KeyFor(string label)
    {
        var slug = new string(label
            .Trim()
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray());

        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        slug = slug.Trim('-');

        return string.IsNullOrEmpty(slug) ? "field" : slug[..Math.Min(slug.Length, 48)];
    }
}
