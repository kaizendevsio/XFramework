using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts.Requests.Create;
using Communications.Integration.Drivers;

namespace IdentityServer.Api.Services;

public sealed class VerificationDeliveryOutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<VerificationDeliveryOutboxDispatcher> logger,
    TimeProvider timeProvider) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private const int MaxAttempts = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval, timeProvider);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchOneAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Verification delivery outbox dispatch failed.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task DispatchOneAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var candidate = await db.Set<VerificationDeliveryOutboxMessage>()
            .IgnoreQueryFilters()
            .Where(message => !message.IsDeleted && message.IsEnabled)
            .Where(message => message.ProcessedAt == null && message.DeadLetteredAt == null)
            .Where(message => message.Attempts < MaxAttempts)
            .Where(message => message.NextAttemptAt == null || message.NextAttemptAt <= now)
            .Where(message => message.LeaseExpiresAt == null || message.LeaseExpiresAt <= now)
            .OrderBy(message => message.CreatedAt)
            .Select(message => new OutboxCandidate(message.Id, message.TenantId, message.RequestId))
            .FirstOrDefaultAsync(ct);
        if (candidate is null)
            return;

        var contextInitializer = scope.ServiceProvider
            .GetRequiredService<ITrustedServiceTargetContextInitializer>();
        var authorization = await contextInitializer.EstablishAsync(
            candidate.TenantId,
            XFrameworkServiceNames.Communications,
            [XFrameworkServiceScopes.BoltService],
            XFrameworkServiceNames.IdentityServer,
            candidate.RequestId,
            ct);
        if (!authorization.IsSuccess)
        {
            logger.LogWarning(
                "Verification delivery outbox candidate {OutboxMessageId} could not establish trusted tenant context: {Error}",
                candidate.Id,
                authorization.Error);
            return;
        }

        await AbandonExpiredClaimAsync(db, candidate.Id, candidate.TenantId, now, ct);

        var leaseOwner = $"{Environment.MachineName}:{Guid.NewGuid():N}";
        var claimed = await db.Set<VerificationDeliveryOutboxMessage>()
            .IgnoreQueryFilters()
            .Where(message => message.Id == candidate.Id && message.TenantId == candidate.TenantId)
            .Where(message => message.Attempts < MaxAttempts)
            .Where(message => message.ProcessedAt == null && message.DeadLetteredAt == null)
            .Where(message => message.NextAttemptAt == null || message.NextAttemptAt <= now)
            .Where(message => message.LeaseExpiresAt == null || message.LeaseExpiresAt <= now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.LeaseOwner, leaseOwner)
                .SetProperty(message => message.LeaseExpiresAt, now.Add(LeaseDuration))
                .SetProperty(message => message.Attempts, message => message.Attempts + 1)
                .SetProperty(message => message.LastAttemptAt, now)
                .SetProperty(message => message.ModifiedAt, now), ct);
        if (claimed == 0)
            return;

        var message = await db.Set<VerificationDeliveryOutboxMessage>()
            .IgnoreQueryFilters()
            .AsTracking()
            .SingleAsync(item =>
                item.Id == candidate.Id &&
                item.TenantId == candidate.TenantId &&
                item.LeaseOwner == leaseOwner, ct);
        ICommunicationsServiceWrapper wrapper;
        try
        {
            wrapper = scope.ServiceProvider.GetRequiredService<ICommunicationsServiceWrapper>();
        }
        catch (Exception ex)
        {
            await ScheduleRetryOrDeadLetterAsync(db, message, timeProvider.GetUtcNow().UtcDateTime, ct);
            ReleaseLease(message, timeProvider.GetUtcNow().UtcDateTime);
            await db.SaveChangesAsync(ct);
            logger.LogWarning(ex, "Verification delivery outbox message {OutboxMessageId} failed before dispatch.", message.Id);
            return;
        }

        message.DispatchStartedAt = timeProvider.GetUtcNow().UtcDateTime;
        message.ModifiedAt = message.DispatchStartedAt.Value;
        message.ConcurrencyStamp = Guid.NewGuid();
        await db.SaveChangesAsync(ct);

        try
        {
            var result = await wrapper.CreateDirectMessageAsync(new CreateDirectMessageRequest
            {
                MessageTransportType = (MessageTransportType)message.TransportType,
                Sender = GenericSender.System,
                Recipient = message.Recipient ?? string.Empty,
                Subject = message.Subject,
                Intent = message.Intent ?? "Verification",
                Message = message.Message ?? string.Empty,
                IsScheduled = false,
                Metadata = new RequestMetadata
                {
                    RequestedTenantId = authorization.Context!.EffectiveTenantId,
                    RequestId = message.RequestId
                }
            }, ct);

            if (result.IsSuccess)
            {
                message.ProcessedAt = timeProvider.GetUtcNow().UtcDateTime;
                message.LastError = null;
                message.NextAttemptAt = null;
                message.DispatchStartedAt = null;
                Redact(message);
            }
            else
            {
                await ScheduleRetryOrDeadLetterAsync(db, message, timeProvider.GetUtcNow().UtcDateTime, ct);
                logger.LogWarning(
                    "Verification delivery outbox message {OutboxMessageId} was rejected before delivery and will {Disposition}.",
                    message.Id,
                    message.DeadLetteredAt is null ? "retry" : "be dead-lettered");
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var failureTime = timeProvider.GetUtcNow().UtcDateTime;
            message.DeadLetteredAt = failureTime;
            message.NextAttemptAt = null;
            message.LastError = "Verification delivery outcome was uncertain; request a new code.";
            await CancelVerificationAsync(db, message.VerificationId, failureTime, ct);
            Redact(message);
            logger.LogWarning(ex, "Verification delivery outbox message {OutboxMessageId} had an uncertain delivery outcome.", message.Id);
        }
        finally
        {
            ReleaseLease(message, timeProvider.GetUtcNow().UtcDateTime);
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task AbandonExpiredClaimAsync(
        DbContext db,
        Guid messageId,
        Guid tenantId,
        DateTime now,
        CancellationToken ct)
    {
        await db.Set<VerificationDeliveryOutboxMessage>()
            .IgnoreQueryFilters()
            .Where(message => message.Id == messageId && message.TenantId == tenantId)
            .Where(message => !message.IsDeleted && message.IsEnabled)
            .Where(message => message.ProcessedAt == null && message.DeadLetteredAt == null)
            .Where(message => message.Attempts < MaxAttempts)
            .Where(message => message.LeaseExpiresAt <= now && message.DispatchStartedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.LeaseOwner, (string?)null)
                .SetProperty(message => message.LeaseExpiresAt, (DateTime?)null)
                .SetProperty(message => message.NextAttemptAt, now)
                .SetProperty(message => message.LastError, "Verification delivery lease expired before dispatch started; retrying.")
                .SetProperty(message => message.ModifiedAt, now), ct);

        var abandonedVerificationIds = await db.Set<VerificationDeliveryOutboxMessage>()
            .IgnoreQueryFilters()
            .Where(message => message.Id == messageId && message.TenantId == tenantId)
            .Where(message => !message.IsDeleted && message.IsEnabled)
            .Where(message => message.ProcessedAt == null && message.DeadLetteredAt == null)
            .Where(message => message.LeaseExpiresAt <= now)
            .Where(message => message.DispatchStartedAt != null || message.Attempts >= MaxAttempts)
            .Select(message => message.VerificationId)
            .ToListAsync(ct);
        if (abandonedVerificationIds.Count == 0)
            return;

        await db.Set<IdentityVerification>()
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId)
            .Where(item => abandonedVerificationIds.Contains(item.Id) && item.ConsumedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, (short?)GenericStatusType.Canceled)
                .SetProperty(item => item.StatusUpdatedOn, now)
                .SetProperty(item => item.ConsumedAt, now)
                .SetProperty(item => item.IsEnabled, false)
                .SetProperty(item => item.ConcurrencyStamp, Guid.NewGuid()), ct);

        await db.Set<VerificationDeliveryOutboxMessage>()
            .IgnoreQueryFilters()
            .Where(message => message.Id == messageId && message.TenantId == tenantId)
            .Where(message => abandonedVerificationIds.Contains(message.VerificationId))
            .Where(message => message.ProcessedAt == null && message.DeadLetteredAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.DeadLetteredAt, now)
                .SetProperty(message => message.Recipient, (string?)null)
                .SetProperty(message => message.Message, (string?)null)
                .SetProperty(message => message.Subject, (string?)null)
                .SetProperty(message => message.Intent, (string?)null)
                .SetProperty(message => message.LastError, "Verification delivery outcome was uncertain; request a new code.")
                .SetProperty(message => message.LeaseOwner, (string?)null)
                .SetProperty(message => message.LeaseExpiresAt, (DateTime?)null)
                .SetProperty(message => message.NextAttemptAt, (DateTime?)null)
                .SetProperty(message => message.ModifiedAt, now), ct);
    }

    private static async Task ScheduleRetryOrDeadLetterAsync(
        DbContext db,
        VerificationDeliveryOutboxMessage message,
        DateTime now,
        CancellationToken ct)
    {
        message.DispatchStartedAt = null;
        message.LastError = "Verification delivery failed before acceptance.";
        if (message.Attempts < MaxAttempts)
        {
            message.NextAttemptAt = now.Add(GetRetryDelay(message.Attempts));
            return;
        }

        message.DeadLetteredAt = now;
        message.NextAttemptAt = null;
        await CancelVerificationAsync(db, message.VerificationId, now, ct);
        Redact(message);
    }

    private static async Task CancelVerificationAsync(
        DbContext db,
        Guid verificationId,
        DateTime now,
        CancellationToken ct)
    {
        var verification = await db.Set<IdentityVerification>()
            .IgnoreQueryFilters()
            .AsTracking()
            .SingleOrDefaultAsync(item => item.Id == verificationId && item.ConsumedAt == null, ct);
        if (verification is null)
            return;

        verification.Status = (short?)GenericStatusType.Canceled;
        verification.StatusUpdatedOn = now;
        verification.ConsumedAt = now;
        verification.IsEnabled = false;
        verification.ConcurrencyStamp = Guid.NewGuid();
    }

    private static void ReleaseLease(VerificationDeliveryOutboxMessage message, DateTime now)
    {
        message.LeaseOwner = null;
        message.LeaseExpiresAt = null;
        message.ModifiedAt = now;
        message.ConcurrencyStamp = Guid.NewGuid();
    }

    private static TimeSpan GetRetryDelay(int attempts) =>
        TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, Math.Clamp(attempts, 1, 6))));

    private static void Redact(VerificationDeliveryOutboxMessage message)
    {
        message.Recipient = null;
        message.Subject = null;
        message.Intent = null;
        message.Message = null;
    }

    private sealed record OutboxCandidate(Guid Id, Guid TenantId, Guid RequestId);
}
