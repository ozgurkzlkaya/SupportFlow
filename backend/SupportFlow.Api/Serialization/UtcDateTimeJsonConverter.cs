using System.Text.Json;
using System.Text.Json.Serialization;

namespace SupportFlow.Api.Serialization;

/// <summary>
/// SQL Server does not retain <see cref="DateTime.Kind"/>. SupportFlow persists
/// timestamps as UTC, so API responses must restore that UTC designator.
/// </summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options) => reader.GetDateTime();

    public override void Write(
        Utf8JsonWriter writer,
        DateTime value,
        JsonSerializerOptions options) =>
        writer.WriteStringValue(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
