using System.Net;
using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts.Requests.Create;
using Communications.Domain.Shared.Contracts.Requests.Templates;
using Communications.Domain.Shared.Contracts.Requests.Update;
using Communications.Domain.Shared.Contracts.Responses;
using Notifications.Domain.Shared.Contracts;
using Notifications.Domain.Shared.Contracts.Requests;
using Notifications.Domain.Shared.Contracts.Responses;
using Notifications.Domain.Shared.Enums;
using Notifications.Integration.Drivers;
using XFramework.Core.Patterns;
using XFramework.Core.Services;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.Enums;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Core.Loggers;
using XFramework.Integration.Services.Helpers;

namespace Communications.Api.Services;

public sealed class CommunicationsService(
    IDataContext dataContext,
    ITenantResolver tenantService,
    INotificationsServiceWrapper notificationsServiceWrapper,
    ICommunicationsTemplateService templateService,
    ICommunicationsRequestContextResolver requestContextResolver,
    ICommunicationsPolicyService policyService,
    ICommunicationsActionRateLimiter rateLimiter,
    ILogger<CommunicationsService> logger
) : ICommunicationsService
{
    private static readonly TimeSpan DeliveryLeaseDuration = TimeSpan.FromMinutes(2);

    public async Task<Result<CmdResponse>> CreateDirectMessageAsync(CreateDirectMessageRequest request, CancellationToken ct = default)
    {
        try
        {
            var tenantContext = await requestContextResolver.ResolveTrustedInternalAsync(
                request.Metadata,
                [XFrameworkServiceNames.Portal, XFrameworkServiceNames.IdentityServer],
                ct);
            if (!tenantContext.IsSuccess)
            {
                return Result<CmdResponse>.Failure(
                    tenantContext.Message ?? "Trusted Communications direct transport context could not be resolved",
                    tenantContext.StatusCode);
            }

            var tenant = await tenantService.GetTenant(tenantContext.Data!.TenantId);
            var requestId = request.Metadata.RequestId;
            var record = requestId is { } existingRequestId
                ? await GetDirectMessageAsync(tenant.Id, existingRequestId, ct)
                : null;
            if (record?.Status is MessageStatus.Sent or MessageStatus.Delivered)
            {
                return AcceptedDirectMessage();
            }

            var now = DateTime.UtcNow;
            if (record?.Status == MessageStatus.Processing &&
                record.ModifiedAt is { } modifiedAt &&
                modifiedAt > now.Subtract(DeliveryLeaseDuration))
            {
                return Result<CmdResponse>.Failure("Direct message delivery is already being processed", 409);
            }

            var policy = await policyService.GetPolicyAsync(tenant.Id, ct);
            var rateLimit = rateLimiter.Check(
                tenant.Id,
                tenantContext.Data.CredentialId ?? Guid.Empty,
                CommunicationsRateLimitActions.DirectExternalTransport,
                policy.DirectExternalTransportPerMinute);
            if (!rateLimit.IsSuccess)
                return Result<CmdResponse>.Failure(
                    rateLimit.Message ?? "Communications direct transport rate limit exceeded",
                    rateLimit.StatusCode);

            if (!Enum.IsDefined(request.MessageTransportType))
            {
                logger.CommunicationsUnknownTransportType(request.MessageTransportType.ToString());
                return Result<CmdResponse>.Failure($"Unknown message transport type: {request.MessageTransportType}", 400);
            }

            if (!TryMapDeliveryChannel(request.MessageTransportType, out var deliveryChannel))
            {
                logger.CommunicationsTransportNotImplemented(request.MessageTransportType.ToString());
                return Result<CmdResponse>.Failure(
                    $"Message transport type {request.MessageTransportType} is not yet supported by Communications direct transport",
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
                        "Message template could not be rendered",
                        renderResult.StatusCode);
                }

                renderedTemplate = renderResult.Data;
                messageText = renderedTemplate.Body;
            }

            if (string.IsNullOrWhiteSpace(messageText))
                return Result<CmdResponse>.Failure("Message text or template is required", 400);

            if (record is null)
            {
                record = new MessageDirect
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    MessageTransportType = request.MessageTransportType,
                    ExternalRecipient = null,
                    Subject = null,
                    Message = "[redacted]",
                    Intent = request.Intent,
                    TemplateId = renderedTemplate?.TemplateId,
                    TemplateKey = renderedTemplate?.TemplateKey,
                    TemplateType = renderedTemplate?.TemplateType,
                    TemplateVariablesJson = "{}",
                    AgentClusterId = request.AgentClusterId,
                    IdempotencyRequestId = requestId,
                    Status = MessageStatus.Processing,
                    ModifiedAt = now
                };

                dataContext.Add(record);
            }
            else
            {
                record.Status = MessageStatus.Processing;
                record.ModifiedAt = now;
                record.ConcurrencyStamp = Guid.NewGuid();
                dataContext.Update(record);
            }

            var saveResult = await dataContext.SaveChangesAsync(ct);
            if (!saveResult.IsSuccess)
            {
                return Result<CmdResponse>.Failure(
                    "Direct message delivery could not be claimed",
                    saveResult.StatusCode == 0 ? 409 : saveResult.StatusCode);
            }

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
                    CorrelationId = $"communications-direct:{record.Id:N}",
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
                record.ConcurrencyStamp = Guid.NewGuid();
                dataContext.Update(record);
                await dataContext.SaveChangesOrThrowAsync(ct);
                logger.CommunicationsCreateDirectError(ex.GetType().Name);
                return Result<CmdResponse>.Failure("Direct message could not be queued with the SMS gateway", 502);
            }

            if (!result.IsSuccess)
            {
                record.Status = MessageStatus.Failed;
                record.ModifiedAt = DateTime.UtcNow;
                record.ConcurrencyStamp = Guid.NewGuid();
                dataContext.Update(record);
                await dataContext.SaveChangesOrThrowAsync(ct);

                var statusCode = result.HttpStatusCode == 0
                    ? 502
                    : (int)result.HttpStatusCode;

                return Result<CmdResponse>.Failure(
                    "Direct message could not be queued with the delivery provider",
                    statusCode);
            }

            record.Status = MessageStatus.Sent;
            record.ModifiedAt = DateTime.UtcNow;
            record.ConcurrencyStamp = Guid.NewGuid();
            dataContext.Update(record);
            var completionSave = await dataContext.SaveChangesAsync(ct);
            if (!completionSave.IsSuccess)
            {
                return Result<CmdResponse>.Failure(
                    "Direct message delivery state could not be finalized",
                    completionSave.StatusCode == 0 ? 503 : completionSave.StatusCode);
            }

            return AcceptedDirectMessage();
        }
        catch (Exception ex)
        {
            logger.CommunicationsCreateDirectError(ex.GetType().Name);
            return OperationFailure(ex, "Direct message could not be queued");
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
                logger.CommunicationsTransportNotImplemented(transportType.Value.ToString());
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
            logger.CommunicationsCreateDirectError(ex.GetType().Name);
            return OperationFailure(ex, "Verification message could not be queued");
        }
    }

    public async Task<Result<CmdResponse>> UpdateMessageDirectAsync(UpdateMessageDirectRequest request, CancellationToken ct = default)
    {
        try
        {
            var tenantContext = await requestContextResolver.ResolveTrustedInternalAsync(
                request.Metadata,
                [XFrameworkServiceNames.SmsGateway],
                ct);
            if (!tenantContext.IsSuccess)
            {
                return Result<CmdResponse>.Failure(
                    tenantContext.Message ?? "Trusted Communications direct transport context could not be resolved",
                    tenantContext.StatusCode);
            }

            var agent = await dataContext.Query<RegistryConfiguration>()
                .Where(x => x.Key == "Settings:Communications:Sms:AgentClusterId")
                .Where(x => x.Value == request.AgentClusterId.ToString())
                .Where(x => x.TenantId == tenantContext.Data!.TenantId)
                .FirstOrDefaultAsync(ct);

            if (agent is null)
            {
                logger.CommunicationsAgentClusterIdNotFound(request.AgentClusterId);
                return Result<CmdResponse>.Failure("Agent cluster id not found");
            }

            var record = await dataContext.Query<MessageDirect>()
                .Where(x => x.TenantId == agent.TenantId)
                .Where(x => x.Id == request.Id)
                .FirstOrDefaultAsync(ct);

            if (record is null)
            {
                logger.CommunicationsMessageNotFound(request.Id ?? Guid.Empty, request.AgentClusterId);
                return Result<CmdResponse>.Failure("Message not found");
            }

            record.Status = MessageStatus.Sent;
            record.AgentClusterId = request.AgentClusterId;
            record.SentAt = request.SentAt;
            record.ReceivedAt = request.ReceivedAt;

            dataContext.Update(record);
            await dataContext.SaveChangesOrThrowAsync(ct);

            return Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "Message updated successfully"
            });
        }
        catch (Exception ex)
        {
            logger.CommunicationsUpdateError(request.Id ?? Guid.Empty, ex);
            return OperationFailure(ex, "Message could not be updated");
        }
    }

    private static bool HasTemplate(Guid? templateId, string? templateKey) =>
        templateId is Guid || !string.IsNullOrWhiteSpace(templateKey);

    private Task<MessageDirect?> GetDirectMessageAsync(Guid tenantId, Guid requestId, CancellationToken ct) =>
        dataContext.Query<MessageDirect>()
            .IgnoreQueryFilters()
            .Where(message => message.TenantId == tenantId)
            .Where(message => message.IdempotencyRequestId == requestId)
            .FirstOrDefaultAsync(ct);

    private static Result<CmdResponse> AcceptedDirectMessage() =>
        Result<CmdResponse>.Success(new CmdResponse
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Direct message delivery queued"
        });

    private static Result<CmdResponse> OperationFailure(Exception exception, string publicMessage) =>
        Result<CmdResponse>.Failure(
            publicMessage,
            exception is CommunicationsPersistenceException persistenceException && persistenceException.StatusCode > 0
                ? persistenceException.StatusCode
                : 500);

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
