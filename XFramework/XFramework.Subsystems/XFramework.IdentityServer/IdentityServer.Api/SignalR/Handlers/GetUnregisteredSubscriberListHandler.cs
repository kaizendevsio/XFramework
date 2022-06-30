using IdentityServer.Core.DataAccess.Query.Entity.Subscription;
using IdentityServer.Domain.Generic.Contracts.Requests.Get.Subscription;
using IdentityServer.Domain.Generic.Contracts.Responses.Subscription;

namespace IdentityServer.Api.SignalR.Handlers;

public class GetUnregisteredSubscriberListHandler : BaseSignalRHandler, ISignalREventHandler
{
    public void Handle(HubConnection connection, IMediator mediator)
    {
        HandleRequestQuery<GetUnregisteredSubscriberListRequest, GetUnregisteredSubscriberListQuery, List<SubscriptionResponse>>(connection, mediator);
    }
}