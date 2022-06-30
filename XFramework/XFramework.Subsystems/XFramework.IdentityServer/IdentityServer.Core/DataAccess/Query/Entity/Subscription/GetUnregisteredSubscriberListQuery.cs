using IdentityServer.Domain.Generic.Contracts.Requests.Get.Subscription;
using IdentityServer.Domain.Generic.Contracts.Responses;
using IdentityServer.Domain.Generic.Contracts.Responses.Subscription;

namespace IdentityServer.Core.DataAccess.Query.Entity.Subscription;

public class GetUnregisteredSubscriberListQuery : GetUnregisteredSubscriberListRequest, IRequest<QueryResponse<List<SubscriptionResponse>>>
{
    
}