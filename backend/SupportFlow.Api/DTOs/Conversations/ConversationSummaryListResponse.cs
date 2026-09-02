namespace SupportFlow.Api.DTOs.Conversations;

public sealed class ConversationSummaryListResponse
{
    public IReadOnlyList<ConversationSummaryResponse> Items { get; init; } = [];
    public ConversationFilterCountsResponse Counts { get; init; } = new();
}

public sealed class ConversationFilterCountsResponse
{
    public int All { get; init; }
    public int AwaitingReply { get; init; }
    public int Unread { get; init; }
    public int Pinned { get; init; }
}
