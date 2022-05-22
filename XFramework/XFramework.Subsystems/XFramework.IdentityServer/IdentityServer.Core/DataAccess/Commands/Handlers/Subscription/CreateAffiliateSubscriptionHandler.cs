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
        
        var subscription = new Domain.DataTransferObjects.Subscription
        {
            Guid = Guid.NewGuid().ToString(),
            EntityId = entity.Id,
            Value = request.Value,
            Status = (short?) AffiliateSubscriptionStatus.Active
        };
        _dataLayer.Subscriptions.Add(subscription);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);

        switch (entity.Name)
        {
            case "SMS":
                var messageTemplate = await _dataLayer.RegistryConfigurations
                    .Where(i => i.ApplicationId == application.Id)
                    .Where(i => i.Group.Name == "MessagingService_Otp")
                    .FirstOrDefaultAsync(CancellationToken.None);

                if (messageTemplate is null)
                {
                    return new()
                    {
                        HttpStatusCode = HttpStatusCode.Conflict,
                        IsSuccess = true,
                        Message = "Unable to send message: OTP message template could not be found"
                    };
                }

                var otp = request.VerificationToken;
                var message = messageTemplate.Value.Replace("|Value|", $"{otp}");
                var contact = request.Value;

                if (string.IsNullOrEmpty(contact))
                {
                    return new()
                    {
                        HttpStatusCode = HttpStatusCode.BadGateway,
                        IsSuccess = false,
                        Message = "Unable to send message: Contact number is empty"
                    };
                }
                
                await _messagingServiceWrapper.CreateDirectMessage(new()
                {
                    MessageType = Guid.Parse("f4fca110-790d-41d7-a0be-b5c699c9a9db"),
                    Sender = "+630000000000",
                    Recipient = contact,
                    Subject = "One Time Password",
                    Intent = "OTP",
                    Message = message,
                    IsScheduled = false
                });
                
                break;
        }
        
        return new(){
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Subscription created",
            IsSuccess = true
        };
    }
}