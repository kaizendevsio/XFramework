using IdentityServer.Core.DataAccess.Query.Entity.Subscription;
using IdentityServer.Domain.Generic.Contracts.Responses;
using IdentityServer.Domain.Generic.Contracts.Responses.Subscription;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Subscription;

public class GetUnregisteredSubscriberListHandler : QueryBaseHandler, IRequestHandler<GetUnregisteredSubscriberListQuery, QueryResponse<List<SubscriptionResponse>>>
{
    public GetUnregisteredSubscriberListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<SubscriptionResponse>>> Handle(GetUnregisteredSubscriberListQuery request, CancellationToken cancellationToken)
    {
        var subscriptions = await _dataLayer.Subscriptions.Where(i =>  i.CredentialId == null).ToListAsync(CancellationToken.None);
        if (subscriptions.Count == 0)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No subscriptions found",
                IsSuccess = true,
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Subscriptions found",
            IsSuccess = true,
            Response = subscriptions.Adapt<List<SubscriptionResponse>>()
        };
    }
}