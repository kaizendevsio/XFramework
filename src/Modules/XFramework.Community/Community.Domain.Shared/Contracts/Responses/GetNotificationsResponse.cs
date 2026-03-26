namespace Community.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record GetNotificationsResponse
{
    public List<NotificationItemResponse> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
}
