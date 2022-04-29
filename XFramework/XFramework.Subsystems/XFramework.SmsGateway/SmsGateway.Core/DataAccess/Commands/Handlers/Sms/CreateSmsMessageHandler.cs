using Messaging.Domin.Generic.Enums;
using SmsGateway.Core.DataAccess.Commands.Entity.Sms;

namespace SmsGateway.Core.DataAccess.Commands.Handlers.Sms;

public class CreateSmsMessageHandler : CommandBaseHandler, IRequestHandler<CreateSmsMessageCmd, CmdResponse<CreateSmsMessageCmd>>
{
    public CreateSmsMessageHandler(IDataLayer dataLayer, ICachingService cachingService)
    {
        _cachingService = cachingService;
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateSmsMessageCmd>> Handle(CreateSmsMessageCmd request, CancellationToken cancellationToken)
    {
        _cachingService.PendingMessageList.Add(new()
        {
            CreatedAt = DateTime.Now,
            ModifiedAt = DateTime.Now,
            IsDeleted = false,
            Sender = request.Sender,
            Recipient = request.Recipient,
            Intent = request.Intent,
            Subject = request.Subject,
            Message = request.Message,
            Guid = $"{request.ClientReference}",
            Status = (int) (request.IsScheduled ? MessageStatus.Scheduled : MessageStatus.Queued)
        });

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };
    }
}