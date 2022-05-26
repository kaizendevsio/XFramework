using IdentityServer.Core.DataAccess.Commands.Entity.Subscription;
using IdentityServer.Domain.Generic.Enums;
using Messaging.Integration.Interfaces;
using XFramework.Integration.Interfaces;

namespace IdentityServer.Core.DataAccess.Commands.Handlers.Subscription;

public class CreateAffiliateSubscriptionHandler : CommandBaseHandler, IRequestHandler<CreateAffiliateSubscriptionCmd, CmdResponse>
{
    private readonly IMessagingServiceWrapper _messagingServiceWrapper;

    public CreateAffiliateSubscriptionHandler(IMessagingServiceWrapper messagingServiceWrapper, IDataLayer dataLayer)
    {
        _messagingServiceWrapper = messagingServiceWrapper;
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse> Handle(CreateAffiliateSubscriptionCmd request, CancellationToken cancellationToken)
    {
        var application = await GetApplication(request.RequestServer.ApplicationId);

        var entity = await _dataLayer.SubscriptionEntities
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.SubscriptionEntityGuid}", CancellationToken.None);
        
        if (entity == null)
        {
            return new(){
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = "Subscription entity not found",
                IsSuccess = false
            };
        }

        var identityContact = await _dataLayer.IdentityContacts.AnyAsync(i => i.Value == request.Value, CancellationToken.None);
        if (identityContact)
        {
            return new(){
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = "Contact is already registered",
                IsSuccess = false
            };
        }

        var subscriptions = _dataLayer.Subscriptions
            .AsNoTracking()
            .Where(i => i.Value == request.Value)
            .Any();
        /*if (subscriptions)
        {
            return new(){
                HttpStatusCode = HttpStatusCode.BadRequest,
                Message = "Subscription already exists",
                IsSuccess = false
            };
        }*/

        if (!subscriptions)
        {
            var subscription = new Domain.DataTransferObjects.Subscription
            {
                Guid = Guid.NewGuid().ToString(),
                EntityId = entity.Id,
                Value = request.Value,
                Status = (short?) AffiliateSubscriptionStatus.Active
            };
            _dataLayer.Subscriptions.Add(subscription);
            await _dataLayer.SaveChangesAsync(CancellationToken.None);
        }

        return new(){
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Subscription created",
            IsSuccess = true
        };
    }
}