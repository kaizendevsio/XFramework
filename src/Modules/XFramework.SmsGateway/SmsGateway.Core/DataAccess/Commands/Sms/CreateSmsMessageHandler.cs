using Microsoft.AspNetCore.SignalR.Client;
using SmsGateway.Domain.Shared.Contracts.Requests.Create;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;

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
        
        if (_cachingService.PendingMessageList.TryAdd(Guid.NewGuid(), data) is false)
        { 
            goto Retry;
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}