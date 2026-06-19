using System.Data;
using Wallets.Domain.Shared.Contracts.Requests;
using Wallets.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;

namespace Wallets.Api.Services;

public sealed class WalletOutboxService(
    DbContext dbContext,
    IWalletRequestContextResolver contextResolver,
    IWalletOutboxPublisher publisher,
    ILogger<WalletOutboxService> logger) : IWalletOutboxService
{
    private readonly string _workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

    public Task<Result<WalletOutboxActionResponse>> RetryAsync(WalletOutboxActionRequest request, CancellationToken ct = default) =>
        UpdateStatusAsync(request, WalletOutboxStatus.Pending, resetAttempts: false, ct);

    public Task<Result<WalletOutboxActionResponse>> ReplayAsync(WalletOutboxActionRequest request, CancellationToken ct = default) =>
        UpdateStatusAsync(request, WalletOutboxStatus.Pending, resetAttempts: true, ct);

    public Task<Result<WalletOutboxActionResponse>> DeadLetterAsync(WalletOutboxActionRequest request, CancellationToken ct = default) =>
        UpdateStatusAsync(request, WalletOutboxStatus.DeadLetter, resetAttempts: false, ct);

    public async Task DispatchDueAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        const int batchSize = 50;
        var pending = (int)WalletOutboxStatus.Pending;
        var failed = (int)WalletOutboxStatus.Failed;

        await using var leaseTransaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var due = await dbContext.Set<WalletOutboxMessage>()
            .FromSqlInterpolated($"""
                SELECT *
                FROM "Wallet"."WalletOutboxMessage"
                WHERE "IsDeleted" = false
                  AND "Status" IN ({pending}, {failed})
                  AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= {now})
                  AND ("LockedUntil" IS NULL OR "LockedUntil" <= {now})
                ORDER BY COALESCE("NextAttemptAt", "CreatedAt")
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .IgnoreQueryFilters()
            .AsTracking()
            .ToListAsync(ct);

        foreach (var message in due)
        {
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
