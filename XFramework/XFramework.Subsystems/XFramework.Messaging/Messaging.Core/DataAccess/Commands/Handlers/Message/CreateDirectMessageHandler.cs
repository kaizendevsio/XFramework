using Messaging.Core.DataAccess.Commands.Entity.Message;
using Messaging.Domin.Generic.Enums;
using SmsGateway.Domain.Generic.Contracts.Requests.Create;
using SmsGateway.Integration.Interfaces;

namespace Messaging.Core.DataAccess.Commands.Handlers.Message;

public class CreateDirectMessageHandler : CommandBaseHandler ,IRequestHandler<CreateDirectMessageCmd, CmdResponse>
{
    private readonly ISmsGatewayServiceWrapper _smsGatewayServiceWrapper;
    private readonly ICachingService _cachingService;

    public CreateDirectMessageHandler(IDataLayer dataLayer, ISmsGatewayServiceWrapper smsGatewayServiceWrapper, ICachingService cachingService)
    {
        _smsGatewayServiceWrapper = smsGatewayServiceWrapper;
        _cachingService = cachingService;
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
        
        var clientReference = Guid.NewGuid();
        request.Sender = request.Sender.ValidatePhoneNumber();
        request.Recipient = request.Recipient.ValidatePhoneNumber();
        
        var createSmsMessageRequest = request.Adapt<CreateSmsMessageRequest>();
        createSmsMessageRequest.ClientReference = $"{clientReference}";
        
        switch (messageType.Guid)
        {
            // Type SMS
            case "f4fca110-790d-41d7-a0be-b5c699c9a9db":
                var result = await _smsGatewayServiceWrapper.CreateSmsMessage(createSmsMessageRequest);
                if (result.HttpStatusCode is not HttpStatusCode.Accepted) return result;
                break;
        }
        
        _cachingService.QueuedMessageList.Add(new()
        {
            CreatedAt = DateTime.Now,
            ModifiedAt = DateTime.Now,
            IsDeleted = false,
            Message = createSmsMessageRequest.Message,
            Guid = createSmsMessageRequest.ClientReference,
            Status = (short) MessageStatus.Queued
        });
        
        MessageDirect? parentMessage = null;
        if (request.ParentMessageGuid is not null)
        {
            parentMessage = await _dataLayer.MessageDirects.SingleOrDefaultAsync(i => i.Guid == $"{request.ParentMessageGuid}", CancellationToken.None);
        }        
        
        var senderIdentityContact = await  _dataLayer.IdentityContacts
            .Include(i => i.UserCredential)
            .AsSplitQuery()
            .SingleOrDefaultAsync(i => i.Value == request.Sender, CancellationToken.None);

        var senderIdentityCredential = senderIdentityContact?.UserCredential;
        
        var recipientIdentityContact = await  _dataLayer.IdentityContacts
            .Include(i => i.UserCredential)
            .AsSplitQuery()
            .SingleOrDefaultAsync(i => i.Value == request.Recipient, CancellationToken.None);

        var recipientIdentityCredential = recipientIdentityContact?.UserCredential;

        _dataLayer.MessageDirects.Add(new()
        {
            Sender = request.Sender,
            Recipient = request.Recipient,
            Intent = request.Intent,
            Subject = request.Subject,
            Message = request.Message,
            Guid = $"{clientReference}",
            Status = (short) MessageStatus.Queued,
            ParentMessage = parentMessage,
            RecipientNavigation = recipientIdentityCredential,
            SenderNavigation = senderIdentityCredential,
            Type = messageType
        });
        await _dataLayer.SaveChangesAsync(CancellationToken.None);

        /*var c = _cachingService.QueuedMessageList.Find(i => i.Guid == $"{createSmsMessageRequest.ClientReference}");
        c.Status = (short) MessageStatus.Sent;*/

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };

    }
}