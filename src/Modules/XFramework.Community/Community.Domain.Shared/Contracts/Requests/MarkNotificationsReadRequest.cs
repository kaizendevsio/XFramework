namespace Community.Domain.Shared.Contracts.Requests;

using TResponse = CmdResponse;

[MemoryPackable]
public partial record MarkNotificationsReadRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<MarkNotificationsReadRequest, TResponse>
{
    public Guid RequestingIdentityId { get; set; }
    public List<Guid> NotificationIds { get; set; } = [];
}
