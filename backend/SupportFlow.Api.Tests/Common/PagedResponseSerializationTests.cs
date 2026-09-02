using System.Text.Json;
using SupportFlow.Api.DTOs.Common;

namespace SupportFlow.Api.Tests.Common;

public sealed class PagedResponseSerializationTests
{
    [Fact]
    public void Serialize_UsesObjectContractInsteadOfCollectionContract()
    {
        var response = new PagedResponse<int>
        {
            Items = [1, 2],
            Pagination = new PaginationMetadata
            {
                Page = 1,
                PageSize = 25,
                TotalCount = 2,
                TotalPages = 1
            }
        };

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(
            response,
            new JsonSerializerOptions(JsonSerializerDefaults.Web)));

        Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        Assert.True(document.RootElement.TryGetProperty("items", out _));
        Assert.True(document.RootElement.TryGetProperty("pagination", out _));
    }
}
