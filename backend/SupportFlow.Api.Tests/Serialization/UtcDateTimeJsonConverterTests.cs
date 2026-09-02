using System.Text.Json;
using SupportFlow.Api.Serialization;

namespace SupportFlow.Api.Tests.Serialization;

public class UtcDateTimeJsonConverterTests
{
    [Fact]
    public void Serialize_UnspecifiedDatabaseTimestamp_WritesUtcDesignator()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new UtcDateTimeJsonConverter());
        var response = new TimestampResponse
        {
            CreatedAt = new DateTime(2026, 9, 1, 17, 13, 0, DateTimeKind.Unspecified)
        };

        var json = JsonSerializer.Serialize(response, options);

        Assert.Contains("\"createdAt\":\"2026-09-01T17:13:00Z\"", json, StringComparison.Ordinal);
    }

    private sealed class TimestampResponse
    {
        public DateTime CreatedAt { get; init; }
    }
}
