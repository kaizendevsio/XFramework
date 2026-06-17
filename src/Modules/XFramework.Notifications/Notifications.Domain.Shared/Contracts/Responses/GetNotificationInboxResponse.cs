namespace Notifications.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record GetNotificationInboxResponse
{
    public List<NotificationInboxItemResponse> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
