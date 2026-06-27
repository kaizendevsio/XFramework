using SmsGateway.Domain.Shared.Contracts;
using SmsGateway.Domain.Shared.Contracts.Requests.Create;
using SmsGateway.Domain.Shared.Contracts.Requests.Get;
using SmsGateway.Domain.Shared.Contracts.Responses.Sms;
using SmsGateway.Domain.Shared.Enums;
using XFramework.Core.Loggers;
using XFramework.Core.Patterns;

namespace SmsGateway.Api.Services;

public sealed class SmsService(
    AppDbContext db,
    ILogger<SmsService> logger,
    IConfiguration configuration) : ISmsService
{
    private readonly TimeSpan _leaseDuration = TimeSpan.FromSeconds(
        Math.Max(30, configuration.GetValue("SmsGateway:LeaseSeconds", 120)));
    private readonly int _maxAttempts = Math.Max(1, configuration.GetValue("SmsGateway:MaxAttempts", 5));

    public async Task<Result<CmdResponse>> ConfirmMessageSentAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            var job = await db.Set<SmsOutboundJob>()
                .AsTracking()
                .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);

            if (job is null)
                return Result<CmdResponse>.Failure("Message not found in pending list", 404);

            if (job.Status is SmsOutboundJobStatus.Sent)
                return Result<CmdResponse>.Success(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });

            var now = DateTime.UtcNow;
            job.Status = SmsOutboundJobStatus.Sent;
            job.SentAt = now;
            job.LeasedUntil = null;
            job.LeaseOwner = null;
            job.ModifiedAt = now;

            var attempt = await db.Set<SmsDeliveryAttempt>()
                .AsTracking()
                .Where(x => x.SmsOutboundJobId == job.Id)
                .OrderByDescending(x => x.AttemptNumber)
                .FirstOrDefaultAsync(ct);

            if (attempt is not null && attempt.CompletedAt is null)
            {
                attempt.Status = SmsOutboundJobStatus.Sent;
                attempt.CompletedAt = now;
                attempt.ModifiedAt = now;
            }

            await db.SaveChangesAsync(ct);

            logger.LogInformation(
                "SMS message {MessageId} confirmed as sent for agent cluster {AgentClusterId}",
                id,
                job.AgentClusterId);

            return Result<CmdResponse>.Success(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });
        }
        catch (Exception ex)
        {
            logger.SmsConfirmationError(id, ex);
            return Result<CmdResponse>.Failure($"Error confirming message: {ex.Message}", 500);
        }
    }

    public Task<Result<CmdResponse>> CreateMessageReceivedAsync(
        CreateMessageReceivedRequest request,
        CancellationToken ct = default)
    {
        try
        {
            logger.LogInformation(
                "SMS message received from {Sender} for agent cluster {AgentClusterId}: {Message}",
                request.Sender,
                request.AgentClusterId,
                request.Message?.Substring(0, Math.Min(50, request.Message?.Length ?? 0)));

            return Task.FromResult(Result<CmdResponse>.Success(new CmdResponse
            {
                HttpStatusCode = HttpStatusCode.OK,
                Message = "Message received recorded"
            }));
        }
        catch (Exception ex)
        {
            logger.SmsCreateMessageReceivedError(request.AgentClusterId, ex);
            return Task.FromResult(Result<CmdResponse>.Failure($"Error creating message received: {ex.Message}", 500));
        }
    }

    public async Task<Result<CmdResponse>> CreateSmsMessageAsync(
        CreateSmsMessageRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var tenantId = request.Metadata.TenantId ?? Guid.Empty;
            if (tenantId == Guid.Empty)
                return Result<CmdResponse>.Failure("Tenant ID is required", 400);

            if (request.AgentClusterId == Guid.Empty)
                return Result<CmdResponse>.Failure("Agent cluster ID is required", 400);

            if (string.IsNullOrWhiteSpace(request.Recipient))
                return Result<CmdResponse>.Failure("Recipient is required", 400);

            if (string.IsNullOrWhiteSpace(request.Message))
                return Result<CmdResponse>.Failure("Message is required", 400);

            var now = DateTime.UtcNow;
            var jobId = request.Id == Guid.Empty ? Guid.NewGuid() : request.Id;
            var existing = await db.Set<SmsOutboundJob>()
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == jobId, ct);

            if (existing is not null)
                return Result<CmdResponse>.Success(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });

            var scheduledAt = request.IsScheduled
                ? request.SendSchedule ?? now
                : request.SendSchedule;

            var job = new SmsOutboundJob
            {
                Id = jobId,
                TenantId = tenantId,
                AgentClusterId = request.AgentClusterId,
                Sender = request.Sender?.Trim(),
                Recipient = request.Recipient.Trim(),
                Subject = request.Subject?.Trim(),
                Intent = request.Intent?.Trim(),
                Message = request.Message.Trim(),
                Status = scheduledAt is not null && scheduledAt > now
                    ? SmsOutboundJobStatus.RetryScheduled
                    : SmsOutboundJobStatus.Queued,
                ScheduledAt = scheduledAt,
                NextAttemptAt = scheduledAt is not null && scheduledAt > now ? scheduledAt : now,
                MaxAttempts = _maxAttempts,
                CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? null : request.CorrelationId.Trim(),
                NotificationDeliveryJobId = request.NotificationDeliveryJobId,
                CreatedAt = now,
                ModifiedAt = now,
                ConcurrencyStamp = Guid.NewGuid(),
                IsEnabled = true
            };

            db.Set<SmsOutboundJob>().Add(job);
            await db.SaveChangesAsync(ct);

            return Result<CmdResponse>.Success(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });
        }
        catch (DbUpdateException ex)
        {
            logger.SmsCreateMessageError(request.AgentClusterId, ex);
            return Result<CmdResponse>.Failure($"Error creating SMS message: {ex.Message}", 500);
        }
        catch (Exception ex)
        {
            logger.SmsCreateMessageError(request.AgentClusterId, ex);
            return Result<CmdResponse>.Failure($"Error creating SMS message: {ex.Message}", 500);
        }
    }

    public async Task<Result<List<SmsNodeJob>>> GetPendingSmsMessagesAsync(
        GetPendingSmsMessageListRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            var jobs = await ReadyJobs(request.AgentClusterId, now)
                .AsNoTracking()
                .OrderBy(x => x.NextAttemptAt)
                .ThenBy(x => x.CreatedAt)
                .Take(100)
                .Select(x => ToNodeJob(x))
                .ToListAsync(ct);

            return Result<List<SmsNodeJob>>.Success(jobs);
        }
        catch (Exception ex)
        {
            logger.SmsGetPendingError(request.AgentClusterId, ex);
            return Result<List<SmsNodeJob>>.Failure($"Error getting pending SMS messages: {ex.Message}", 500);
        }
    }

    public async Task<Result<List<SmsNodeJob>>> GetScheduledSmsMessagesAsync(
        GetScheduledSmsMessageListRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var jobs = await db.Set<SmsOutboundJob>()
                .AsNoTracking()
                .Where(x => x.AgentClusterId == request.AgentClusterId)
                .Where(x => !x.IsDeleted)
                .Where(x => x.Status == SmsOutboundJobStatus.RetryScheduled)
                .OrderBy(x => x.NextAttemptAt)
                .Take(100)
                .Select(x => ToNodeJob(x))
                .ToListAsync(ct);

            return Result<List<SmsNodeJob>>.Success(jobs);
        }
        catch (Exception ex)
        {
            logger.SmsGetScheduledError(request.AgentClusterId, ex);
            return Result<List<SmsNodeJob>>.Failure($"Error getting scheduled SMS messages: {ex.Message}", 500);
        }
    }

    public async Task<Result<List<SmsNodeJob>>> GetPendingWithStatusUpdateAsync(
        Guid agentClusterId,
        CancellationToken ct = default)
    {
        try
        {
            var now = DateTime.UtcNow;
            var leaseOwner = $"sms-node:{agentClusterId:N}";
            var jobs = await ReadyJobs(agentClusterId, now)
                .AsTracking()
                .OrderBy(x => x.NextAttemptAt)
                .ThenBy(x => x.CreatedAt)
                .Take(25)
                .ToListAsync(ct);

            foreach (var job in jobs)
            {
                job.Status = SmsOutboundJobStatus.Leased;
                job.LeasedUntil = now.Add(_leaseDuration);
                job.LeaseOwner = leaseOwner;
                job.AttemptCount += 1;
                job.ModifiedAt = now;

                db.Set<SmsDeliveryAttempt>().Add(new SmsDeliveryAttempt
                {
                    Id = Guid.NewGuid(),
                    TenantId = job.TenantId,
                    SmsOutboundJobId = job.Id,
                    AttemptNumber = job.AttemptCount,
                    Status = SmsOutboundJobStatus.Leased,
                    LeaseOwner = leaseOwner,
                    StartedAt = now,
                    CreatedAt = now,
                    ModifiedAt = now,
                    ConcurrencyStamp = Guid.NewGuid(),
                    IsEnabled = true
                });
            }

            if (jobs.Count > 0)
                await db.SaveChangesAsync(ct);

            return Result<List<SmsNodeJob>>.Success(jobs.Select(ToNodeJob).ToList());
        }
        catch (Exception ex)
        {
            logger.SmsGetPendingError(agentClusterId, ex);
            return Result<List<SmsNodeJob>>.Failure($"Error getting pending SMS messages with status update: {ex.Message}", 500);
        }
    }

    private IQueryable<SmsOutboundJob> ReadyJobs(Guid agentClusterId, DateTime now) =>
        db.Set<SmsOutboundJob>()
            .Where(x => x.AgentClusterId == agentClusterId)
            .Where(x => !x.IsDeleted)
            .Where(x => x.NextAttemptAt == null || x.NextAttemptAt <= now)
            .Where(x =>
                x.Status == SmsOutboundJobStatus.Queued ||
                x.Status == SmsOutboundJobStatus.RetryScheduled ||
                (x.Status == SmsOutboundJobStatus.Leased && x.LeasedUntil < now));

    private static SmsNodeJob ToNodeJob(SmsOutboundJob job) => new()
    {
        Id = job.Id,
        TenantId = job.TenantId,
        CreatedAt = job.CreatedAt,
        ModifiedAt = job.ModifiedAt,
        IsDeleted = job.IsDeleted,
        IsEnabled = job.IsEnabled,
        AgentClusterId = job.AgentClusterId,
        Recipient = job.Recipient,
        Message = job.Message,
        Status = job.Status switch
        {
            SmsOutboundJobStatus.Sent => MessageStatus.Sent,
            SmsOutboundJobStatus.Failed or SmsOutboundJobStatus.DeadLettered => MessageStatus.Failed,
            SmsOutboundJobStatus.RetryScheduled => MessageStatus.Scheduled,
            SmsOutboundJobStatus.Leased or SmsOutboundJobStatus.Sending => MessageStatus.Processing,
            SmsOutboundJobStatus.Cancelled => MessageStatus.Blocked,
            _ => MessageStatus.Queued
        }
    };
}
