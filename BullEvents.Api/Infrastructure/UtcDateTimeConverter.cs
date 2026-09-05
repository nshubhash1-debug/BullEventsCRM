using System.Text.Json;
using System.Text.Json.Serialization;

namespace BullEvents.Api.Infrastructure;

/// <summary>
/// Reads and writes every <see cref="DateTime"/> as UTC, with the trailing Z.
///
/// Everything is stored in UTC, but MySQL hands values back with
/// <see cref="DateTimeKind.Unspecified"/>. System.Text.Json then serialises them
/// without an offset, and the browser parses an offset-less timestamp as *local*
/// time — so an Indian user saw every timestamp shifted five and a half hours
/// into the past. Stamping the kind on the way out fixes it at the boundary
/// rather than asking each client to guess.
/// </summary>
public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetDateTime();

        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        DateTime value,
        JsonSerializerOptions options)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

        writer.WriteStringValue(utc.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ"));
    }
}

/// <summary>The same treatment for nullable columns.</summary>
public class NullableUtcDateTimeConverter : JsonConverter<DateTime?>
{
    private static readonly UtcDateTimeConverter Inner = new();

    public override DateTime? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Null
            ? null
            : Inner.Read(ref reader, typeof(DateTime), options);

    public override void Write(
        Utf8JsonWriter writer,
        DateTime? value,
        JsonSerializerOptions options)
    {
        if (value is null) writer.WriteNullValue();
        else Inner.Write(writer, value.Value, options);
    }
}
