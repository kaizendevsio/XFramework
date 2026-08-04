using System.Data;
using IdentityServer.Domain.Shared.Contracts;
using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Security;

namespace Wallets.Api.Services;

public sealed class WalletOutboxService(
    DbContext dbContext,
    IWalletRequestContextResolver contextResolver,
    IWalletFeatureGateService featureGateService,
    IWalletOutboxPublisher publisher,
    ITrustedInvocationContextAccessor invocationContextAccessor,
    ILogger<WalletOutboxService> logger) : IWalletOutboxService
{
    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    public Task<Result<WalletOutboxActionResponse>> RetryAsync(WalletOutboxActionRequest request, CancellationToken ct = default) =>
        UpdateStatusAsync(request, WalletOutboxStatus.Pending, resetAttempts: false, ct);

    public Task<Result<WalletOutboxActionResponse>> ReplayAsync(WalletOutboxActionRequest request, CancellationToken ct = default) =>
        UpdateStatusAsync(request, WalletOutboxStatus.Pending, resetAttempts: true, ct);

    public Task<Result<WalletOutboxActionResponse>> DeadLetterAsync(WalletOutboxActionRequest request, CancellationToken ct = default) =>
        UpdateStatusAsync(request, WalletOutboxStatus.DeadLetter, resetAttempts: false, ct);

    public async Task<IReadOnlyList<Guid>> GetDueTenantIdsAsync(CancellationToken ct = default)
    {
        EnsureTenantDiscoveryAuthorization();
        var now = DateTime.UtcNow;
        return await dbContext.Set<WalletOutboxMessage>()
            .IgnoreQueryFilters()
            .Where(message => !message.IsDeleted &&
                ((message.Status == WalletOutboxStatus.Pending || message.Status == WalletOutboxStatus.Failed) &&
                 (message.NextAttemptAt == null || message.NextAttemptAt <= now) &&
                 (message.LockedUntil == null || message.LockedUntil <= now) ||
                 message.Status == WalletOutboxStatus.Processing &&
                 message.LockedUntil != null &&
                 message.LockedUntil <= now))
            .Select(message => message.TenantId)
            .Distinct()
            .Take(50)
            .ToListAsync(ct);
    }

    private void EnsureTenantDiscoveryAuthorization()
    {
        var context = invocationContextAccessor.Current;
        if (context?.Actor is not null ||
            context?.EffectiveTenantId is not null ||
            context?.RequestedTargetTenantId is not null ||
            context?.Service is not { } service ||
            !string.Equals(service.ClientId, XFrameworkServiceNames.Wallets, StringComparison.Ordinal) ||
            !service.Scopes.Contains(XFrameworkServiceScopes.WalletsAdmin) ||
            !service.Scopes.Contains(XFrameworkServiceScopes.DataContextQueryAllTenants))
        {
            throw new UnauthorizedAccessException(
                "Wallet outbox tenant discovery requires the authorized Wallets service identity.");
        }
    }

    public async Task DispatchDueAsync(CancellationToken ct = default)
    {
        var tenantId = ResolveDispatchTenantId();

        var now = DateTime.UtcNow;
        const int batchSize = 50;
        var pending = (int)WalletOutboxStatus.Pending;
        var failed = (int)WalletOutboxStatus.Failed;
        var processing = (int)WalletOutboxStatus.Processing;

        await using var leaseTransaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var due = await dbContext.Set<WalletOutboxMessage>()
            .FromSqlInterpolated($"""
                SELECT *
                FROM "Wallet"."WalletOutboxMessage"
                WHERE "IsDeleted" = false
                  AND "TenantId" = {tenantId}
                  AND (
                    (
                      "Status" IN ({pending}, {failed})
                      AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= {now})
                      AND ("LockedUntil" IS NULL OR "LockedUntil" <= {now})
                    )
                    OR (
                      "Status" = {processing}
                      AND "LockedUntil" IS NOT NULL
                      AND "LockedUntil" <= {now}
                    )
                  )
                ORDER BY COALESCE("NextAttemptAt", "CreatedAt")
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .IgnoreQueryFilters()
            .AsTracking()
            .ToListAsync(ct);

        foreach (var message in due)
        {
            if (message.Status == WalletOutboxStatus.Processing)
            {
                message.LastError = "Recovered expired outbox processing lease";
            }

            message.Status = WalletOutboxStatus.Processing;
            message.LockedBy = _workerId;
            message.LockedUntil = now.AddMinutes(2);
            message.LastAttemptAt = now;
        }

        if (due.Count == 0)
        {
            await leaseTransaction.CommitAsync(ct);
            return;
        }

        await dbContext.SaveChangesAsync(ct);
        await leaseTransaction.CommitAsync(ct);

        foreach (var message in due)
        {
            try
            {
                await publisher.PublishAsync(message, ct);
                message.Status = WalletOutboxStatus.Published;
                message.PublishedAt = DateTime.UtcNow;
                message.LockedBy = null;
                message.LockedUntil = null;
                message.LastError = null;
            }
            catch (Exception ex)
            {
                message.Attempts++;
                message.LastError = ex.Message.Length > 4000 ? ex.Message[..4000] : ex.Message;
                message.LockedBy = null;
                message.LockedUntil = null;
                if (message.Attempts >= message.MaxAttempts)
                {
                    message.Status = WalletOutboxStatus.DeadLetter;
                    message.DeadLetteredAt = DateTime.UtcNow;
                }
                else
                {
                    message.Status = WalletOutboxStatus.Failed;
                    message.NextAttemptAt = DateTime.UtcNow.AddSeconds(Math.Min(Math.Pow(2, message.Attempts) * 15, 900));
                }

                logger.LogWarning(ex, "Wallet outbox message {OutboxMessageId} publish failed", message.Id);
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }

    private Guid ResolveDispatchTenantId()
    {
        var context = invocationContextAccessor.Current;
        if (context?.EffectiveTenantId is not { } tenantId || tenantId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Wallet outbox dispatch requires an authorized service target tenant.");
        }

        if (context.Actor is not null ||
            context.Service is not { } service ||
            !string.Equals(service.ClientId, XFrameworkServiceNames.Wallets, StringComparison.Ordinal) ||
            !service.Scopes.Contains(XFrameworkServiceScopes.TenantTarget) ||
            !service.Scopes.Contains(XFrameworkServiceScopes.WalletsAdmin))
        {
            throw new UnauthorizedAccessException(
                "Wallet outbox dispatch requires the Wallets background-service identity.");
        }

        return tenantId;
    }

    private async Task<Result<WalletOutboxActionResponse>> UpdateStatusAsync(
        WalletOutboxActionRequest request,
        WalletOutboxStatus status,
        bool resetAttempts,
        CancellationToken ct)
    {
        var contextResult = contextResolver.Resolve(request);
        if (!contextResult.IsSuccess)
        {
            return Result<WalletOutboxActionResponse>.Failure(contextResult.Message!, contextResult.StatusCode);
        }

        var feature = await featureGateService.EnsureEnabledAsync(
            contextResult.Data!.TenantId,
            TenantModuleFeatureKeys.WalletsWebhooks,
            ct);
        if (!feature.IsSuccess)
        {
            return Result<WalletOutboxActionResponse>.Failure(feature.Message!, feature.StatusCode);
        }

        var message = await dbContext.Set<WalletOutboxMessage>()
            .IgnoreQueryFilters()
            .AsTracking()
            .FirstOrDefaultAsync(x =>
                x.Id == request.OutboxMessageId &&
                x.TenantId == contextResult.Data!.TenantId &&
                !x.IsDeleted,
                ct);
        if (message is null)
        {
            return Result<WalletOutboxActionResponse>.NotFound("Outbox message not found");
        }

        message.Status = status;
        message.LockedBy = null;
        message.LockedUntil = null;
        message.NextAttemptAt = status == WalletOutboxStatus.Pending ? DateTime.UtcNow : message.NextAttemptAt;
        message.DeadLetteredAt = status == WalletOutboxStatus.DeadLetter ? DateTime.UtcNow : null;
        if (resetAttempts)
        {
            message.Attempts = 0;
            message.LastError = null;
        }

        await dbContext.SaveChangesAsync(ct);
        return Result<WalletOutboxActionResponse>.Success(ToResponse(message, $"Outbox message moved to {status}"));
    }

    private static WalletOutboxActionResponse ToResponse(WalletOutboxMessage message, string? statusMessage = null) =>
        new()
        {
            OutboxMessageId = message.Id,
            Status = message.Status,
            Attempts = message.Attempts,
            NextAttemptAt = message.NextAttemptAt,
            Message = statusMessage
        };
}
