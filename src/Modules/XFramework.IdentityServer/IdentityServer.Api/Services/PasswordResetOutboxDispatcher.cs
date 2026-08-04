namespace IdentityServer.Api.Services;

public interface IPasswordResetProcessor
{
    Task<Result> ProcessForgotPasswordAsync(
        Guid tenantId,
        ForgotPasswordRequest request,
        CancellationToken ct);
}

public sealed class PasswordResetOutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<PasswordResetOutboxDispatcher> logger,
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
                await DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Password reset outbox dispatch failed.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task DispatchBatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DbContext>();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var leaseOwner = $"{Environment.MachineName}:{Guid.NewGuid():N}";

        var candidate = await db.Set<PasswordResetOutboxMessage>()
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
            XFrameworkServiceNames.IdentityServer,
            [],
            XFrameworkServiceNames.IdentityServer,
            candidate.RequestId,
            ct);
        if (!authorization.IsSuccess)
        {
            logger.LogWarning(
                "Password reset outbox candidate {OutboxMessageId} could not establish trusted tenant context: {Error}",
                candidate.Id,
                authorization.Error);
            return;
        }

        await RecoverExpiredClaimAsync(db, candidate.Id, candidate.TenantId, now, ct);

        var leaseExpiresAt = now.Add(LeaseDuration);
        await db.Set<PasswordResetOutboxMessage>()
            .IgnoreQueryFilters()
            .Where(message => message.Id == candidate.Id && message.TenantId == candidate.TenantId)
            .Where(message => message.ProcessedAt == null && message.DeadLetteredAt == null)
            .Where(message => message.LeaseExpiresAt == null || message.LeaseExpiresAt <= now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.LeaseOwner, leaseOwner)
                .SetProperty(message => message.LeaseExpiresAt, leaseExpiresAt)
                .SetProperty(message => message.Attempts, message => message.Attempts + 1)
                .SetProperty(message => message.LastAttemptAt, now)
                .SetProperty(message => message.ModifiedAt, now), ct);

        var due = await db.Set<PasswordResetOutboxMessage>()
            .IgnoreQueryFilters()
            .AsTracking()
            .Where(message => message.Id == candidate.Id && message.TenantId == candidate.TenantId)
            .Where(message => message.LeaseOwner == leaseOwner)
            .OrderBy(message => message.CreatedAt)
            .ToListAsync(ct);
        foreach (var message in due)
        {
            IPasswordResetProcessor processor;
            try
            {
                processor = scope.ServiceProvider.GetRequiredService<IPasswordResetProcessor>();
            }
            catch (Exception ex)
            {
                ScheduleRetryOrDeadLetter(message, timeProvider.GetUtcNow().UtcDateTime);
                logger.LogWarning(ex, "Password reset outbox message {OutboxMessageId} failed before dispatch.", message.Id);
                ReleaseLease(message, timeProvider.GetUtcNow().UtcDateTime);
                continue;
            }

            message.DispatchStartedAt = timeProvider.GetUtcNow().UtcDateTime;
            message.ModifiedAt = message.DispatchStartedAt.Value;
            message.ConcurrencyStamp = Guid.NewGuid();
            await db.SaveChangesAsync(ct);

            try
            {
                var result = await processor.ProcessForgotPasswordAsync(
                    message.TenantId,
                    new ForgotPasswordRequest
                    {
                        Email = message.Email,
                        Phone = message.Phone,
                        Metadata = new RequestMetadata
                        {
                            RequestId = message.RequestId
                        }
                    },
                    ct);

                if (result.IsSuccess)
                {
                    message.ProcessedAt = timeProvider.GetUtcNow().UtcDateTime;
                    message.Email = null;
                    message.Phone = null;
                    message.LastError = null;
                    message.NextAttemptAt = null;
                    message.DispatchStartedAt = null;
                }
                else
                {
                    ScheduleRetryOrDeadLetter(message, timeProvider.GetUtcNow().UtcDateTime);
                    logger.LogWarning(
                        "Password reset outbox message {OutboxMessageId} was rejected before delivery and will {Disposition}.",
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
                DeadLetterAmbiguous(message, timeProvider.GetUtcNow().UtcDateTime);
                logger.LogWarning(ex, "Password reset outbox message {OutboxMessageId} had an uncertain processing outcome.", message.Id);
            }
            finally
            {
                ReleaseLease(message, timeProvider.GetUtcNow().UtcDateTime);
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task RecoverExpiredClaimAsync(
        DbContext db,
        Guid messageId,
        Guid tenantId,
        DateTime now,
        CancellationToken ct)
    {
        await db.Set<PasswordResetOutboxMessage>()
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
                .SetProperty(message => message.LastError, "Password reset dispatch lease expired before processing started; retrying.")
                .SetProperty(message => message.ModifiedAt, now), ct);

        await db.Set<PasswordResetOutboxMessage>()
            .IgnoreQueryFilters()
            .Where(message => message.Id == messageId && message.TenantId == tenantId)
            .Where(message => !message.IsDeleted && message.IsEnabled)
            .Where(message => message.ProcessedAt == null && message.DeadLetteredAt == null)
            .Where(message => message.LeaseExpiresAt <= now)
            .Where(message => message.DispatchStartedAt != null || message.Attempts >= MaxAttempts)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(message => message.DeadLetteredAt, now)
                .SetProperty(message => message.Email, (string?)null)
                .SetProperty(message => message.Phone, (string?)null)
                .SetProperty(message => message.LastError, "Password reset processing outcome was uncertain; submit a new request.")
                .SetProperty(message => message.LeaseOwner, (string?)null)
                .SetProperty(message => message.LeaseExpiresAt, (DateTime?)null)
                .SetProperty(message => message.NextAttemptAt, (DateTime?)null)
                .SetProperty(message => message.ModifiedAt, now), ct);
    }

    private static void ScheduleRetryOrDeadLetter(PasswordResetOutboxMessage message, DateTime now)
    {
        message.DispatchStartedAt = null;
        message.LastError = "Password reset processing failed before delivery.";
        if (message.Attempts >= MaxAttempts)
        {
            message.DeadLetteredAt = now;
            message.NextAttemptAt = null;
            message.Email = null;
            message.Phone = null;
            return;
        }

        message.NextAttemptAt = now.Add(GetRetryDelay(message.Attempts));
    }

    private static void DeadLetterAmbiguous(PasswordResetOutboxMessage message, DateTime now)
    {
        message.DeadLetteredAt = now;
        message.NextAttemptAt = null;
        message.Email = null;
        message.Phone = null;
        message.LastError = "Password reset processing outcome was uncertain; submit a new request.";
    }

    private static void ReleaseLease(PasswordResetOutboxMessage message, DateTime now)
    {
        message.LeaseOwner = null;
        message.LeaseExpiresAt = null;
        message.ModifiedAt = now;
        message.ConcurrencyStamp = Guid.NewGuid();
    }

    private static TimeSpan GetRetryDelay(int attempts) =>
        TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, Math.Clamp(attempts, 1, 6))));

    private sealed record OutboxCandidate(Guid Id, Guid TenantId, Guid RequestId);
}
