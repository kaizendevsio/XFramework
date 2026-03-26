namespace Messaging.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record GetThreadMessagesResponse
{
    public List<ThreadMessageItemResponse> Items { get; set; } = [];
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}
