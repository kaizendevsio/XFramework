using System.Net;
using MediatR;
using Messaging.Domain.Shared.Contracts.Requests.Create;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmsGateway.Integration.Drivers;
using XFramework.Core.Services;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Enums;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Services.Helpers;

namespace Messaging.Core.Commands;

public class CreateDirectMessage(
    DbContext dbContext,
    IHelperService helperService,
    ITenantService tenantService,
    ISmsGatewayServiceWrapper smsGatewayServiceWrapper,
    ILogger<CreateDirectMessage> logger
) : IRequestHandler<CreateDirectMessageRequest, CmdResponse>
{
    public async Task<CmdResponse> Handle(CreateDirectMessageRequest request, CancellationToken cancellationToken)
    {
        var tenant = await tenantService.GetTenant(request.Metadata.TenantId);

        var record = new MessageDirect()
        {
            TenantId = tenant.Id,
            MessageTransportType = MessageTransportType.Sms,
            ExternalRecipient = request.Recipient,
            Message = request.Message,
            Status = MessageStatus.Queued
        };

        await dbContext.AddAsync(record, CancellationToken.None);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        
        switch (request.MessageTransportType)
        {
            case MessageTransportType.Sms:
            {
                var configuration = await dbContext.Set<RegistryConfiguration>()
                    .Where(x => x.TenantId == tenant.Id)
                    .Where(x => x.Key == "Settings:Messaging:Sms:AgentClusterId")
                    .FirstOrDefaultAsync(CancellationToken.None);
                
                var agentClusterId = configuration?.Value ?? throw new("Agent cluster id not found");
                
                var result = await smsGatewayServiceWrapper.CreateSmsMessage(new()
                {
                    Id = record.Id,
                    AgentClusterId = new Guid(agentClusterId),
                    Recipient = request.Recipient.ValidatePhoneNumber(convertOnly: true),
                    Message = request.Message
                });

                return result;
                break;
            }
            case MessageTransportType.Email:
            case MessageTransportType.Push:
            case MessageTransportType.Webhook:
                throw new NotImplementedException();
            default:
                throw new ArgumentOutOfRangeException();
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.OK,
            Message = "Message sent"
        };
    }
}