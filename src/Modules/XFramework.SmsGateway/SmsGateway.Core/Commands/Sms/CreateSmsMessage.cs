using SmsGateway.Domain.Shared.Contracts.Requests.Create;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;

namespace SmsGateway.Core.Commands.Sms;

public class CreateSmsMessage(ICachingService cachingService) : IRequestHandler<CreateSmsMessageRequest, CmdResponse>
{
    public async Task<CmdResponse> Handle(CreateSmsMessageRequest request, CancellationToken cancellationToken)
    {
        Retry:
        var data = new SmsNodeJob()
        {
            Id = request.Id,
            CreatedAt = DateTime.Now,
            ModifiedAt = DateTime.Now,
            IsDeleted = false,
            AgentClusterId = request.AgentClusterId,
            Recipient = request.Recipient,
            Message = request.Message
        };
        
        if (cachingService.PendingMessageList.TryAdd(Guid.NewGuid(), data) is false)
        { 
            goto Retry;
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}