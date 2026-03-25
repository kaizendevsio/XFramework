using System.Net;
using Messaging.Domain.Shared.Contracts.Requests.Create;
using Messaging.Domain.Shared.Contracts.Requests.Update;
using Microsoft.Extensions.Logging;
using SmsGateway.Integration.Drivers;
using XFramework.Core.Patterns;
using XFramework.Core.Services;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.Enums;
using XFramework.Integration.Services.Helpers;
using XFramework.Core.Loggers;

namespace Messaging.Api.Services;

public sealed class MessagingService(
    IDataContext dataContext,
    IHelperService helperService,
    ITenantResolver tenantService,
    ISmsGatewayServiceWrapper smsGatewayServiceWrapper,
    ILogger<MessagingService> logger
) : IMessagingService
{
    public async Task<Result<CmdResponse>> CreateDirectMessageAsync(CreateDirectMessageRequest request, CancellationToken ct = default)
    {
        try
        {
            var tenant = await tenantService.GetTenant(request.Metadata.TenantId);

            var configuration = await dataContext.Query<RegistryConfiguration>()
                .Where(x => x.TenantId == tenant.Id)
                .Where(x => x.Key == "Settings:Messaging:Sms:AgentClusterId")
                .FirstOrDefaultAsync(ct);

            var agentClusterId = string.Empty;
            if (request.AgentClusterId != Guid.Empty)
            {
                agentClusterId = request.AgentClusterId.ToString();
            }
            else
            {
                if (configuration?.Value == null)
                {
                    logger.MessagingAgentClusterNotFound(tenant.Id);
                    return Result<CmdResponse>.Failure("Agent cluster id not found");
                }
                agentClusterId = configuration.Value;
            }

            var record = new MessageDirect()
            {
                TenantId = tenant.Id,
                MessageTransportType = MessageTransportType.Sms,
                ExternalRecipient = request.Recipient,
                Message = request.Message,
                AgentClusterId = Guid.Parse(agentClusterId),
                Status = MessageStatus.Queued
            };

            dataContext.Add(record);
            await dataContext.SaveChangesAsync(ct);

            switch (request.MessageTransportType)
            {
                case MessageTransportType.Sms:
                {
                    var result = await smsGatewayServiceWrapper.CreateSmsMessage(new()
                    {
                        Id = record.Id,
                        AgentClusterId = new Guid(agentClusterId),
                        Recipient = request.Recipient.ValidatePhoneNumber(convertOnly: true),
                        Message = request.Message
                    });

                    return Result<CmdResponse>.Success(result);
                }
                case MessageTransportType.Email:
                case MessageTransportType.Push:
                case MessageTransportType.Webhook:
                    logger.MessagingTransportNotImplemented(request.MessageTransportType.ToString());
                    return Result<CmdResponse>.Failure($"Message transport type {request.MessageTransportType} not implemented");

                default:
                    logger.MessagingUnknownTransportType(request.MessageTransportType.ToString());
                    return Result<CmdResponse>.Failure($"Unknown message transport type: {request.MessageTransportType}");
            }
        }
        catch (Exception ex)
        {
            logger.MessagingCreateDirectError(request.Recipient, ex);
            return Result<CmdResponse>.Failure($"Error creating direct message: {ex.Message}");
        }
    }

    public async Task<Result<CmdResponse>> UpdateMessageDirectAsync(UpdateMessageDirectRequest request, CancellationToken ct = default)
    {
        try
        {
            var agent = await dataContext.Query<RegistryConfiguration>()
                .Where(x => x.Key == "Settings:Messaging:Sms:AgentClusterId")
                .Where(x => x.Value == request.AgentClusterId.ToString())
                .FirstOrDefaultAsync(ct);

            if (agent is null)
            {
                logger.MessagingAgentClusterIdNotFound(request.AgentClusterId);
                return Result<CmdResponse>.Failure("Agent cluster id not found");
            }

            var record = await dataContext.Query<MessageDirect>()
                .Where(x => x.TenantId == agent.TenantId)
                .Where(x => x.Id == request.Id)
                .FirstOrDefaultAsync(ct);

            if (record is null)
            {
                logger.MessagingMessageNotFound(request.Id ?? Guid.Empty, request.AgentClusterId);
                return Result<CmdResponse>.Failure("Message not found");
            }

            record.Status = MessageStatus.Sent;
            record.AgentClusterId = request.AgentClusterId;
            record.SentAt = request.SentAt;
            record.RecievedAt = request.RecievedAt;

            dataContext.Update(record);
            await dataContext.SaveChangesAsync(ct);

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "Message updated successfully"
            });
        }
        catch (Exception ex)
        {
            logger.MessagingUpdateError(request.Id ?? Guid.Empty, ex);
            return Result<CmdResponse>.Failure($"Error updating message: {ex.Message}");
        }
    }
}
