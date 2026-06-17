namespace Notifications.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record GetNotificationInboxRequest : RequestBase,
    IQuery<QueryResponse<GetNotificationInboxResponse>>,
    IBoltRequest<GetNotificationInboxRequest, QueryResponse<GetNotificationInboxResponse>>
{
    public Guid? TenantId { get; set; }
    public Guid RecipientCredentialId { get; set; }
    public bool? IsRead { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
