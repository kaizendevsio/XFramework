using System.Net;
using MediatR;
using Messaging.Core.Commands;
using Messaging.Domain.Shared.Contracts.Requests.Update;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmsGateway.Integration.Drivers;
using XFramework.Core.Services;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.Enums;
using XFramework.Integration.Abstractions;

namespace Messaging.Core.Queries;

public class UpdateMessageDirect(
    DbContext dbContext,
    IHelperService helperService,
    ITenantService tenantService,
    ISmsGatewayServiceWrapper smsGatewayServiceWrapper,
    ILogger<CreateDirectMessage> logger
) : IRequestHandler<UpdateMessageDirectRequest, CmdResponse>
{
    public async Task<CmdResponse> Handle(UpdateMessageDirectRequest request, CancellationToken cancellationToken)
    {
        var agent = await dbContext.Set<RegistryConfiguration>()
            .Where(x => x.Key == "Settings:Messaging:Sms:AgentClusterId")
            .Where(x => x.Value == request.AgentClusterId.ToString())
            .FirstOrDefaultAsync(CancellationToken.None);

        if (agent is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NotFound,
                Message = "Agent cluster id not found"
            };
        }     
        
        var record = await dbContext.Set<MessageDirect>()
            .Where(x => x.TenantId == agent.TenantId)
            .Where(x => x.Id == request.Id)
            .FirstOrDefaultAsync(CancellationToken.None);
        
        if (record is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        record.Status = MessageStatus.Sent;
        record.AgentClusterId = request.AgentClusterId;
        record.SentAt = request.SentAt;
        record.RecievedAt = request.RecievedAt;
        
        await dbContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}