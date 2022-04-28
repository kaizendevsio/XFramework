using Messaging.Core.DataAccess.Commands.Entity.Message;
using SmsGateway.Domain.Generic.Contracts.Requests.Create;
using SmsGateway.Integration.Interfaces;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class CreateDirectMessageHandler : CommandBaseHandler ,IRequestHandler<CreateDirectMessageCmd, CmdResponse>
{
    private readonly ISmsGatewayServiceWrapper _smsGatewayServiceWrapper;

    public CreateDirectMessageHandler(IDataLayer dataLayer, ISmsGatewayServiceWrapper smsGatewayServiceWrapper)
    {
        _smsGatewayServiceWrapper = smsGatewayServiceWrapper;
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse> Handle(CreateDirectMessageCmd request, CancellationToken cancellationToken)
    {
        var messageType = await _dataLayer.MessageTypes.FirstOrDefaultAsync(i => i.Guid == $"{request.MessageType}", CancellationToken.None);
        if (messageType == null)
        {
            return new ()
            {
                Message = $"Message type with guid {request.MessageType} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound,
                IsSuccess = false
            };
        }

        
        
        
        //_dataLayer.IdentityContacts.FirstOrDefaultAsync(i => i.Value == )
        
        
        var clientReference = Guid.NewGuid();
        var createSmsMessageRequest = request.Adapt<CreateSmsMessageRequest>();
        createSmsMessageRequest.ClientReference = $"{clientReference}";

        _dataLayer.MessageDirects.Add(new()
        {
            Sender = 0,
            RecipientId = null,
            Sender = null,
            Recipient = null,
            Intent = null,
            Subject = null,
            Message = null,
            Guid = null,
            Status = 0,
            ParentMessage = null,
            RecipientNavigation = null,
            SenderNavigation = null,
            Type = messageType,
            InverseParentMessage = null
        });
        
        switch (messageType.Guid)
        {
            // Type SMS
            case "f4fca110-790d-41d7-a0be-b5c699c9a9db":
                var result = await _smsGatewayServiceWrapper.CreateSmsMessage(createSmsMessageRequest);
                if (result.HttpStatusCode is not HttpStatusCode.Accepted) return result;
                break;
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };

    }
}