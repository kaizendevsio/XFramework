using Messaging.Domain.Shared.Enums;
using SmsGateway.Domain.Shared.Contracts.Requests.Create;

namespace SmsGateway.Core.DataAccess.Commands.Sms;

public class CreateSmsMessageHandler : IRequestHandler<CreateSmsMessageRequest, CmdResponse>
{
    private readonly ICachingService _cachingService;

    public CreateSmsMessageHandler(ICachingService cachingService)
    {
        _cachingService = cachingService;
    }

    public async Task<CmdResponse> Handle(CreateSmsMessageRequest request, CancellationToken cancellationToken)
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
            Guid = $"{request.ReferenceNumber}",
            Status = (int)(request.IsScheduled ? MessageStatus.Scheduled : MessageStatus.Queued)
        });
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}