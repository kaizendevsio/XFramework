using System.Net;
using System.Text.Json;
using Messaging.Domain.Shared;
using Messaging.Domain.Shared.Contracts.Requests.Create;
using Messaging.Domain.Shared.Contracts.Requests.Templates;
using Messaging.Domain.Shared.Contracts.Requests.Update;
using Messaging.Domain.Shared.Contracts.Responses;
using Notifications.Domain.Shared.Contracts;
using Notifications.Domain.Shared.Contracts.Requests;
using Notifications.Domain.Shared.Contracts.Responses;
using Notifications.Domain.Shared.Enums;
using Notifications.Integration.Drivers;
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
    INotificationsServiceWrapper notificationsServiceWrapper,
    IMessagingTemplateService templateService,
    IMessagingRequestContextResolver requestContextResolver,
    IMessagingPolicyService policyService,
    IMessagingActionRateLimiter rateLimiter,
    ILogger<MessagingService> logger
) : IMessagingService
{
    public async Task<Result<CmdResponse>> CreateDirectMessageAsync(CreateDirectMessageRequest request, CancellationToken ct = default)
    {
        try
        {
            var tenantContext = requestContextResolver.ResolveTrustedInternal(
                request.Metadata,
                "ControlPanel",
                "IdentityServer");
            if (!tenantContext.IsSuccess)
            {
                return Result<CmdResponse>.Failure(
                    tenantContext.Message ?? "Trusted Messaging direct transport context could not be resolved",
                    tenantContext.StatusCode);
            }

            var tenant = await tenantService.GetTenant(tenantContext.Data!.TenantId);
            var policy = await policyService.GetPolicyAsync(tenant.Id, ct);
            var rateLimit = rateLimiter.Check(
                tenant.Id,
                tenantContext.Data.CredentialId ?? Guid.Empty,
                MessagingRateLimitActions.DirectExternalTransport,
                policy.DirectExternalTransportPerMinute);
            if (!rateLimit.IsSuccess)
                return Result<CmdResponse>.Failure(
                    rateLimit.Message ?? "Messaging direct transport rate limit exceeded",
                    rateLimit.StatusCode);

            if (!Enum.IsDefined(request.MessageTransportType))
            {
                logger.MessagingUnknownTransportType(request.MessageTransportType.ToString());
                return Result<CmdResponse>.Failure($"Unknown message transport type: {request.MessageTransportType}", 400);
            }

            if (!TryMapDeliveryChannel(request.MessageTransportType, out var deliveryChannel))
            {
                logger.MessagingTransportNotImplemented(request.MessageTransportType.ToString());
                return Result<CmdResponse>.Failure(
                    $"Message transport type {request.MessageTransportType} is not yet supported by Messaging direct transport",
                    501);
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
                MessageTransportType = request.MessageTransportType,
                ExternalRecipient = request.Recipient,
                Subject = renderedTemplate?.Subject ?? request.Subject,
                Message = messageText,
                TemplateId = renderedTemplate?.TemplateId,
                TemplateKey = renderedTemplate?.TemplateKey,
                TemplateType = renderedTemplate?.TemplateType,
                TemplateVariablesJson = JsonSerializer.Serialize(
                    renderedTemplate?.TemplateVariables ?? new Dictionary<string, string>()),
                AgentClusterId = request.AgentClusterId,
                Status = MessageStatus.Queued
            };

            dataContext.Add(record);
            var saveResult = await dataContext.SaveChangesAsync(ct);
            if (!saveResult.IsSuccess)
                return Result<CmdResponse>.Failure(saveResult.Message ?? "Direct message could not be queued", saveResult.StatusCode);

            QueryResponse<NotificationInboxItemResponse> result;
            try
            {
                result = await notificationsServiceWrapper.CreateNotification(new CreateNotificationRequest
                {
                    TenantId = tenant.Id,
                    RecipientCredentialId = tenantContext.Data.CredentialId ?? Guid.Empty,
                    TemplateKey = renderedTemplate?.TemplateKey ?? request.TemplateKey ?? NotificationTemplateKeys.SystemGeneric,
                    Title = renderedTemplate?.Subject ?? request.Subject ?? request.Intent,
                    Body = messageText,
                    DeliveryChannels = deliveryChannel,
                    DeliveryAddress = NormalizeDeliveryAddress(request.MessageTransportType, request.Recipient),
                    CorrelationId = $"messaging-direct:{record.Id:N}",
                    Data = new Dictionary<string, string>
                    {
                        ["MessageDirectId"] = record.Id.ToString(),
                        ["Transport"] = request.MessageTransportType.ToString(),
                        ["Intent"] = request.Intent
                    },
                    Metadata = request.Metadata
                }, ct);
            }
            catch (Exception ex)
            {
                record.Status = MessageStatus.Failed;
                record.ModifiedAt = DateTime.UtcNow;
                dataContext.Update(record);
                await dataContext.SaveChangesAsync(ct);
                logger.MessagingCreateDirectError(request.Recipient, ex);
                return Result<CmdResponse>.Failure("Direct message could not be queued with the SMS gateway", 502);
            }

            if (!result.IsSuccess)
            {
                record.Status = MessageStatus.Failed;
                record.ModifiedAt = DateTime.UtcNow;
                dataContext.Update(record);
                await dataContext.SaveChangesAsync(ct);

                var statusCode = result.HttpStatusCode == 0
                    ? 502
                    : (int)result.HttpStatusCode;

                return Result<CmdResponse>.Failure(
                    result.Message ?? "Direct message could not be queued with the SMS gateway",
                    statusCode);
            }

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Message = "Direct message delivery queued"
            });
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
            var tenantContext = requestContextResolver.ResolveTrustedInternal(
                request.Metadata,
                "XFramework.SmsGateway");
            if (!tenantContext.IsSuccess)
            {
                return Result<CmdResponse>.Failure(
                    tenantContext.Message ?? "Trusted Messaging direct transport context could not be resolved",
                    tenantContext.StatusCode);
            }

            var agent = await dataContext.Query<RegistryConfiguration>()
                .Where(x => x.Key == "Settings:Messaging:Sms:AgentClusterId")
                .Where(x => x.Value == request.AgentClusterId.ToString())
                .Where(x => x.TenantId == tenantContext.Data!.TenantId)
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

    private static bool TryMapDeliveryChannel(
        MessageTransportType transportType,
        out NotificationDeliveryChannel channel)
    {
        channel = transportType switch
        {
            MessageTransportType.Email => NotificationDeliveryChannel.Email,
            MessageTransportType.Sms => NotificationDeliveryChannel.Sms,
            MessageTransportType.Webhook => NotificationDeliveryChannel.Webhook,
            _ => NotificationDeliveryChannel.None
        };

        return channel != NotificationDeliveryChannel.None;
    }

    private static string NormalizeDeliveryAddress(MessageTransportType transportType, string recipient) =>
        transportType == MessageTransportType.Sms
            ? recipient.ValidatePhoneNumber(convertOnly: true)
            : recipient.Trim();
}
