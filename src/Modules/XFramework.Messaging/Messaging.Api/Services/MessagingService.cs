using System.Net;
using System.Text.Json;
using Messaging.Domain.Shared;
using Messaging.Domain.Shared.Contracts.Requests.Create;
using Messaging.Domain.Shared.Contracts.Requests.Templates;
using Messaging.Domain.Shared.Contracts.Requests.Update;
using Messaging.Domain.Shared.Contracts.Responses;
using SmsGateway.Integration.Drivers;
using XFramework.Core.Patterns;
using XFramework.Core.Services;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.Enums;
using XFramework.Core.Loggers;
using XFramework.Integration.Services.Helpers;

namespace Messaging.Api.Services;

public sealed class MessagingService(
    IDataContext dataContext,
    ITenantResolver tenantService,
    ISmsGatewayServiceWrapper smsGatewayServiceWrapper,
    IMessagingTemplateService templateService,
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

            var messageText = request.Message?.Trim();
            RenderMessageTemplateResponse? renderedTemplate = null;
            if (HasTemplate(request.TemplateId, request.TemplateKey))
            {
                var renderResult = await templateService.RenderTemplateAsync(new RenderMessageTemplateRequest
                {
                    Metadata = request.Metadata,
                    TemplateId = request.TemplateId,
                    TemplateKey = request.TemplateKey,
                    TemplateVariables = request.TemplateVariables
                }, ct);

                if (!renderResult.IsSuccess || renderResult.Data is null)
                {
                    return Result<CmdResponse>.Failure(
                        renderResult.Message ?? "Message template could not be rendered",
                        renderResult.StatusCode);
                }

                renderedTemplate = renderResult.Data;
                messageText = renderedTemplate.Body;
            }

            if (string.IsNullOrWhiteSpace(messageText))
                return Result<CmdResponse>.Failure("Message text or template is required", 400);

            var record = new MessageDirect()
            {
                TenantId = tenant.Id,
                MessageTransportType = MessageTransportType.Sms,
                ExternalRecipient = request.Recipient,
                Subject = renderedTemplate?.Subject ?? request.Subject,
                Message = messageText,
                TemplateId = renderedTemplate?.TemplateId,
                TemplateKey = renderedTemplate?.TemplateKey,
                TemplateType = renderedTemplate?.TemplateType,
                TemplateVariablesJson = JsonSerializer.Serialize(
                    renderedTemplate?.TemplateVariables ?? new Dictionary<string, string>()),
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
                        Message = messageText
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

    public async Task<Result<CmdResponse>> CreateVerificationMessageAsync(CreateVerificationMessageRequest request, CancellationToken ct = default)
    {
        try
        {
            MessageTransportType? transportType = request.ContactType switch
            {
                GenericContactType.Phone => MessageTransportType.Sms,
                GenericContactType.Email => MessageTransportType.Email,
                _ => null
            };

            if (transportType is null)
                return Result<CmdResponse>.Failure($"Verification contact type {request.ContactType} not supported");

            if (transportType.Value != MessageTransportType.Sms)
            {
                logger.MessagingTransportNotImplemented(transportType.Value.ToString());
                return Result<CmdResponse>.Failure($"Verification transport type {transportType.Value} not implemented", 501);
            }

            return await CreateDirectMessageAsync(new CreateDirectMessageRequest
            {
                Metadata = request.Metadata,
                MessageTransportType = transportType.Value,
                Recipient = request.Contact!,
                TemplateKey = MessageTemplateKeys.IdentityOtp,
                TemplateVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Value"] = request.VerificationToken!
                },
                Intent = MessageIntents.Verification
            }, ct);
        }
        catch (Exception ex)
        {
            logger.MessagingCreateDirectError(request.Contact ?? string.Empty, ex);
            return Result<CmdResponse>.Failure($"Error creating verification message: {ex.Message}");
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
            record.ReceivedAt = request.ReceivedAt;

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

    private static bool HasTemplate(Guid? templateId, string? templateKey) =>
        templateId is Guid || !string.IsNullOrWhiteSpace(templateKey);
}
