using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IdentityServer.Domain.Shared.Contracts;
using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Enums;

namespace Wallets.Api.Services;

public sealed class WalletPaymentWebhookService(
    DbContext dbContext,
    IConfiguration configuration,
    IHttpContextAccessor httpContextAccessor,
    IWalletFeatureGateService featureGateService,
    IWalletWorkflowService workflowService) : IWalletPaymentWebhookService
{
    public async Task<Result<WalletWebhookIngestResponse>> IngestAsync(
        IngestWalletPaymentWebhookRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderKey) ||
            string.IsNullOrWhiteSpace(request.ExternalEventId))
        {
            return Result<WalletWebhookIngestResponse>.Failure("Provider key and external event id are required", 400);
        }

        var signatureValid = ValidateSignature(request);
        var configuredTenantId = ResolveConfiguredTenantId(request.ProviderKey);
        if (!signatureValid)
        {
            if (configuredTenantId.HasValue)
            {
                dbContext.Set<WalletPaymentWebhookEvent>().Add(CreateWebhookEvent(
                    request,
                    configuredTenantId.Value,
                    signatureValid: false,
                    MapStatus(request.ProviderStatus)));
                await dbContext.SaveChangesAsync(ct);
            }

            return Result<WalletWebhookIngestResponse>.Failure("Webhook signature validation failed", 401);
        }

        var signedPayloadTenantId = ResolveTenantIdFromSignedPayload(request.RawPayloadJson);
        var tenantId = configuredTenantId ?? signedPayloadTenantId;
        if (tenantId is null || tenantId.Value == Guid.Empty)
        {
            return Result<WalletWebhookIngestResponse>.Failure("Webhook tenant context is required", 400);
        }

        if (request.Metadata.TenantId is { } metadataTenantId &&
            metadataTenantId != Guid.Empty &&
            metadataTenantId != tenantId.Value)
        {
            return Result<WalletWebhookIngestResponse>.Forbidden("Webhook tenant does not match trusted provider context");
        }

        var feature = await featureGateService.EnsureEnabledAsync(
            tenantId.Value,
            TenantModuleFeatureKeys.WalletsWebhooks,
            ct);
        if (!feature.IsSuccess)
        {
            return Result<WalletWebhookIngestResponse>.Failure(feature.Message!, feature.StatusCode);
        }

        request.Metadata.TenantId = tenantId.Value;
        if (httpContextAccessor.HttpContext is { } httpContext)
        {
            httpContext.Items["TenantId"] = tenantId.Value;
            httpContext.Items["WalletsPrivilegedActor"] = true;
        }

        var duplicate = await dbContext.Set<WalletPaymentWebhookEvent>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId.Value &&
                !x.IsDeleted &&
                x.ProviderKey == request.ProviderKey &&
                x.ExternalEventId == request.ExternalEventId,
                ct);
        if (duplicate is not null)
        {
            return Result<WalletWebhookIngestResponse>.Success(new WalletWebhookIngestResponse
            {
                WebhookEventId = duplicate.Id,
                Status = WalletWebhookProcessingStatus.Duplicate,
                Duplicate = true,
                DepositRequestId = duplicate.DepositRequestId,
                WithdrawalRequestId = duplicate.WithdrawalRequestId,
                OperationId = duplicate.OperationId,
                Message = "Duplicate webhook ignored"
            });
        }

        var mappedStatus = MapStatus(request.ProviderStatus);
        var webhookEvent = CreateWebhookEvent(request, tenantId.Value, signatureValid: true, mappedStatus);
        dbContext.Set<WalletPaymentWebhookEvent>().Add(webhookEvent);

        try
        {
            var deposit = await FindDepositAsync(tenantId.Value, request, ct);
            var withdrawal = deposit is null
                ? await FindWithdrawalAsync(tenantId.Value, request, ct)
                : null;

            if (deposit is null && withdrawal is null)
            {
                webhookEvent.ProcessingStatus = WalletWebhookProcessingStatus.Failed;
                webhookEvent.ProcessingError = "No matching deposit or withdrawal request was found";
                await dbContext.SaveChangesAsync(ct);
                return Result<WalletWebhookIngestResponse>.NotFound("No matching wallet workflow found");
            }

            var action = new WalletWorkflowActionRequest
            {
                RequestId = deposit?.Id ?? withdrawal!.Id,
                ProviderEventId = request.ExternalEventId,
                ProviderTransactionId = request.ProviderTransactionId,
                ProviderStatus = request.ProviderStatus,
                ExternalReference = request.ExternalReference,
                RawProviderPayloadJson = request.RawPayloadJson,
                IdempotencyKey = $"webhook:{request.ProviderKey}:{request.ExternalEventId}",
                WebhookEventId = webhookEvent.Id,
                Metadata = request.Metadata
            };

            Result<WalletWorkflowResponse> workflowResult;
            if (deposit is not null)
            {
                workflowResult = await DispatchDepositWebhookAsync(mappedStatus, action, ct);
                webhookEvent.DepositRequestId = deposit.Id;
            }
            else
            {
                workflowResult = await DispatchWithdrawalWebhookAsync(mappedStatus, action, ct);
                webhookEvent.WithdrawalRequestId = withdrawal!.Id;
            }

            if (!workflowResult.IsSuccess)
            {
                webhookEvent.OperationId = workflowResult.Data?.OperationId;
                webhookEvent.ProcessingStatus = WalletWebhookProcessingStatus.Failed;
                webhookEvent.ProcessingError = workflowResult.Message;
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(ct);
                return Result<WalletWebhookIngestResponse>.Failure(workflowResult.Message!, workflowResult.StatusCode);
            }

            webhookEvent.OperationId = workflowResult.Data?.OperationId ?? webhookEvent.OperationId;
            if (webhookEvent.ProcessingStatus == WalletWebhookProcessingStatus.Processing)
            {
                webhookEvent.ProcessingStatus = WalletWebhookProcessingStatus.Processed;
                webhookEvent.ProcessingError = null;
                webhookEvent.ProcessedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(ct);
            }

            return Result<WalletWebhookIngestResponse>.Success(new WalletWebhookIngestResponse
            {
                WebhookEventId = webhookEvent.Id,
                Status = webhookEvent.ProcessingStatus,
                Duplicate = false,
                DepositRequestId = webhookEvent.DepositRequestId,
                WithdrawalRequestId = webhookEvent.WithdrawalRequestId,
                OperationId = webhookEvent.OperationId,
                Message = "Webhook processed"
            });
        }
        catch (Exception ex)
        {
            webhookEvent.ProcessingStatus = WalletWebhookProcessingStatus.Failed;
            webhookEvent.ProcessingError = ex.Message;
            webhookEvent.ProcessedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(ct);
            return Result<WalletWebhookIngestResponse>.Failure("Webhook processing failed", 500);
        }
    }

    private Guid? ResolveConfiguredTenantId(string providerKey)
    {
        var configured = configuration[$"Wallets:Webhooks:{providerKey}:TenantId"]
            ?? configuration["Wallets:Webhooks:DefaultTenantId"];

        return Guid.TryParse(configured, out var tenantId) && tenantId != Guid.Empty
            ? tenantId
            : null;
    }

    private static Guid? ResolveTenantIdFromSignedPayload(string rawPayloadJson)
    {
        if (string.IsNullOrWhiteSpace(rawPayloadJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(rawPayloadJson);
            var root = document.RootElement;
            return TryGetGuid(root, "tenantId")
                ?? TryGetGuid(root, "TenantId")
                ?? TryGetGuid(root, "tenant_id");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Guid? TryGetGuid(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) ||
            property.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return Guid.TryParse(property.GetString(), out var id) && id != Guid.Empty
            ? id
            : null;
    }

    private static WalletPaymentWebhookEvent CreateWebhookEvent(
        IngestWalletPaymentWebhookRequest request,
        Guid tenantId,
        bool signatureValid,
        WalletWorkflowStatus mappedStatus) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ProviderKey = request.ProviderKey,
            ExternalEventId = request.ExternalEventId,
            ExternalReference = request.ExternalReference,
            ProviderTransactionId = request.ProviderTransactionId,
            ProviderStatus = request.ProviderStatus,
            MappedWorkflowStatus = mappedStatus,
            SignatureValid = signatureValid,
            SignatureScheme = "hmac-sha256",
            HeadersHash = ComputeHash(JsonSerializer.Serialize(request.Headers)),
            RawPayloadJson = string.IsNullOrWhiteSpace(request.RawPayloadJson) ? "{}" : request.RawPayloadJson,
            ReceivedAt = DateTime.UtcNow,
            ProcessingStatus = signatureValid ? WalletWebhookProcessingStatus.Processing : WalletWebhookProcessingStatus.Rejected,
            ProcessingError = signatureValid ? null : "Webhook signature validation failed"
        };

    private bool ValidateSignature(IngestWalletPaymentWebhookRequest request)
    {
        var secret = configuration[$"Wallets:Webhooks:{request.ProviderKey}:Secret"]
            ?? configuration["Wallets:Webhooks:SharedSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            return false;
        }

        var supplied = request.Signature
            ?? request.Headers.GetValueOrDefault("x-wallet-signature")
            ?? request.Headers.GetValueOrDefault("x-signature");
        if (string.IsNullOrWhiteSpace(supplied))
        {
            return false;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(request.RawPayloadJson));
        var expected = Convert.ToHexString(hash).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(supplied.Trim().ToLowerInvariant()));
    }

    private static WalletWorkflowStatus MapStatus(string? providerStatus) =>
        providerStatus?.Trim().ToLowerInvariant() switch
        {
            "paid" or "ok" or "success" or "succeeded" or "settled" or "completed" => WalletWorkflowStatus.Completed,
            "cancelled" or "canceled" => WalletWorkflowStatus.Cancelled,
            "expired" => WalletWorkflowStatus.Expired,
            "rejected" => WalletWorkflowStatus.Rejected,
            _ => WalletWorkflowStatus.Failed
        };

    private Task<Result<WalletWorkflowResponse>> DispatchDepositWebhookAsync(
        WalletWorkflowStatus mappedStatus,
        WalletWorkflowActionRequest request,
        CancellationToken ct) =>
        mappedStatus switch
        {
            WalletWorkflowStatus.Completed => workflowService.SettleDepositAsync(request, ct),
            WalletWorkflowStatus.Cancelled or WalletWorkflowStatus.Expired => workflowService.CancelDepositAsync(
                request with { Reason = request.Reason ?? $"Provider status: {request.ProviderStatus ?? mappedStatus.ToString()}" },
                ct),
            WalletWorkflowStatus.Rejected => workflowService.RejectDepositAsync(
                request with { Reason = request.Reason ?? $"Provider status: {request.ProviderStatus ?? mappedStatus.ToString()}" },
                ct),
            _ => workflowService.FailDepositAsync(
                request with { Reason = request.Reason ?? $"Provider status: {request.ProviderStatus ?? mappedStatus.ToString()}" },
                ct)
        };

    private Task<Result<WalletWorkflowResponse>> DispatchWithdrawalWebhookAsync(
        WalletWorkflowStatus mappedStatus,
        WalletWorkflowActionRequest request,
        CancellationToken ct) =>
        mappedStatus switch
        {
            WalletWorkflowStatus.Completed => workflowService.SettleWithdrawalAsync(request, ct),
            WalletWorkflowStatus.Cancelled or WalletWorkflowStatus.Expired => workflowService.CancelWithdrawalAsync(
                request with { Reason = request.Reason ?? $"Provider status: {request.ProviderStatus ?? mappedStatus.ToString()}" },
                ct),
            WalletWorkflowStatus.Rejected => workflowService.RejectWithdrawalAsync(
                request with { Reason = request.Reason ?? $"Provider status: {request.ProviderStatus ?? mappedStatus.ToString()}" },
                ct),
            _ => workflowService.FailWithdrawalAsync(
                request with { Reason = request.Reason ?? $"Provider status: {request.ProviderStatus ?? mappedStatus.ToString()}" },
                ct)
        };

    private async Task<DepositRequest?> FindDepositAsync(
        Guid tenantId,
        IngestWalletPaymentWebhookRequest request,
        CancellationToken ct)
    {
        var externalReference = request.ExternalReference?.Trim();
        var providerTransactionId = request.ProviderTransactionId?.Trim();
        if (string.IsNullOrWhiteSpace(externalReference) && string.IsNullOrWhiteSpace(providerTransactionId))
        {
            return null;
        }

        return await dbContext.Set<DepositRequest>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                !x.IsDeleted &&
                ((!string.IsNullOrWhiteSpace(externalReference) &&
                  (x.ExternalReference == externalReference || x.ReferenceNo == externalReference)) ||
                 (!string.IsNullOrWhiteSpace(providerTransactionId) &&
                  x.ProviderTransactionId == providerTransactionId)),
                ct);
    }

    private async Task<WithdrawalRequest?> FindWithdrawalAsync(
        Guid tenantId,
        IngestWalletPaymentWebhookRequest request,
        CancellationToken ct)
    {
        var externalReference = request.ExternalReference?.Trim();
        var providerTransactionId = request.ProviderTransactionId?.Trim();
        if (string.IsNullOrWhiteSpace(externalReference) && string.IsNullOrWhiteSpace(providerTransactionId))
        {
            return null;
        }

        return await dbContext.Set<WithdrawalRequest>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                !x.IsDeleted &&
                ((!string.IsNullOrWhiteSpace(externalReference) &&
                  (x.ExternalReference == externalReference || x.ReferenceNumber == externalReference)) ||
                 (!string.IsNullOrWhiteSpace(providerTransactionId) &&
                  x.ProviderTransactionId == providerTransactionId)),
                ct);
    }

    private static string ComputeHash(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash);
    }
}
