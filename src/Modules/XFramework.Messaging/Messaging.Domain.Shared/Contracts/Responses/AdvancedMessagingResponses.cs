namespace Messaging.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record UnreadThreadCountResponse
{
    public Guid ThreadId { get; set; }
    public int UnreadCount { get; set; }
}

[MemoryPackable]
public partial record GetUnreadCountsResponse
{
    public List<UnreadThreadCountResponse> Threads { get; set; } = [];
    public int TotalUnreadCount { get; set; }
}

[MemoryPackable]
public partial record SearchMessagesResponse
{
    public List<SearchMessageItemResponse> Items { get; set; } = [];
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

[MemoryPackable]
public partial record SearchMessageItemResponse
{
    public Guid ThreadId { get; set; }
    public Guid MessageId { get; set; }
    public Guid SenderCredentialId { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
