using Notifications.Domain.Shared.Contracts;
using SmsGateway.Domain.Shared.Contracts.Requests.Create;
using SmsGateway.Integration.Drivers;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Core.Patterns;

namespace Notifications.Api.Services;

public sealed class NotificationDeliveryDispatcher(
    AppDbContext db,
    ISmsGatewayServiceWrapper smsGateway,
    ILogger<NotificationDeliveryDispatcher> logger,
    IConfiguration configuration)
{
    private readonly TimeSpan _leaseDuration = TimeSpan.FromSeconds(
        Math.Max(30, configuration.GetValue("Notifications:Delivery:LeaseSeconds", 120)));
    private readonly int _batchSize = Math.Clamp(configuration.GetValue("Notifications:Delivery:BatchSize", 25), 1, 100);
    private readonly int _maxAttempts = Math.Max(1, configuration.GetValue("Notifications:Delivery:MaxAttempts", 5));

    public async Task<int> DispatchDueAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var dueJobs = await db.Set<NotificationDeliveryJob>()
            .AsTracking()
            .Where(x => !x.IsDeleted)
            .Where(x => x.Status == NotificationDeliveryStatus.Queued || x.Status == NotificationDeliveryStatus.Failed)
            .Where(x => x.NextAttemptAt == null || x.NextAttemptAt <= now)
            .OrderBy(x => x.NextAttemptAt)
            .ThenBy(x => x.CreatedAt)
            .Take(_batchSize)
            .ToListAsync(ct);

        foreach (var job in dueJobs)
        {
            job.LeasedUntil = now.Add(_leaseDuration);
            job.LeaseOwner = Environment.MachineName;
            job.AttemptCount += 1;
            job.Status = NotificationDeliveryStatus.Queued;
            job.ModifiedAt = now;

            db.Set<NotificationDeliveryAttempt>().Add(new NotificationDeliveryAttempt
            {
                Id = Guid.NewGuid(),
                TenantId = job.TenantId,
                NotificationDeliveryJobId = job.Id,
                AttemptNumber = job.AttemptCount,
                Status = NotificationDeliveryStatus.Queued,
                ProviderKey = job.ProviderKey,
                StartedAt = now,
                CreatedAt = now,
                ModifiedAt = now,
                ConcurrencyStamp = Guid.NewGuid(),
                IsEnabled = true
            });
        }

        if (dueJobs.Count > 0)
            await db.SaveChangesAsync(ct);

        var processed = 0;
        foreach (var job in dueJobs)
        {
            ct.ThrowIfCancellationRequested();
            var result = job.Channel switch
            {
                NotificationDeliveryChannel.Sms => await DispatchSmsAsync(job, ct),
                NotificationDeliveryChannel.Email => await DispatchEmailAsync(job, ct),
                NotificationDeliveryChannel.Webhook => await DispatchWebhookAsync(job, ct),
                _ => Result.Failure("Unsupported delivery channel", 400)
            };

            if (result.IsSuccess)
                processed++;
        }

        return processed;
    }

    private async Task<Result> DispatchSmsAsync(NotificationDeliveryJob job, CancellationToken ct)
    {
        var inbox = await db.Set<NotificationInboxItem>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == job.NotificationInboxItemId && x.TenantId == job.TenantId, ct);

        if (inbox is null)
            return await MarkFailedAsync(job, "not-found", "Notification inbox item was not found", false, ct);

        var agentClusterId = configuration.GetValue<Guid?>("Notifications:Delivery:Sms:AgentClusterId") ?? Guid.Empty;
        if (agentClusterId == Guid.Empty)
            return await MarkProviderPendingAsync(job, "SMS agent cluster is not configured", ct);

        var enqueue = await smsGateway.CreateSmsMessage(new CreateSmsMessageRequest
        {
            Id = Guid.NewGuid(),
            AgentClusterId = agentClusterId,
            Recipient = job.RecipientAddress ?? inbox.RecipientCredentialId.ToString(),
            Subject = inbox.Title,
            Message = inbox.Body,
            Intent = inbox.TemplateKey,
            CorrelationId = job.CorrelationId,
            NotificationDeliveryJobId = job.Id,
            Metadata = new RequestMetadata
            {
                RequestedTenantId = job.TenantId
            }
        });

        if (!enqueue.IsSuccess)
            return await MarkFailedAsync(job, "sms-enqueue-failed", enqueue.Message ?? "SMS enqueue failed", true, ct);

        await MarkSentAsync(job, "sms-gateway", ct);
        return Result.Success();
    }

    private async Task<Result> MarkProviderPendingAsync(NotificationDeliveryJob job, string reason, CancellationToken ct)
    {
        logger.LogInformation(
            "Notification delivery job {DeliveryJobId} for channel {Channel} is waiting for provider configuration: {Reason}",
            job.Id,
            job.Channel,
            reason);

        return await MarkFailedAsync(job, "provider-not-configured", reason, true, ct);
    }

    private async Task<Result> DispatchEmailAsync(NotificationDeliveryJob job, CancellationToken ct)
    {
        var provider = await GetDefaultProviderAsync(job.TenantId, NotificationDeliveryChannel.Email, ct);
        if (provider?.SettingsJson is null)
            return await MarkProviderPendingAsync(job, "SMTP provider is not configured", ct);

        var settings = JsonSerializer.Deserialize<SmtpProviderSettings>(provider.SettingsJson);
        if (settings is null ||
            string.IsNullOrWhiteSpace(settings.Host) ||
            string.IsNullOrWhiteSpace(settings.FromAddress) ||
            string.IsNullOrWhiteSpace(job.RecipientAddress))
        {
            return await MarkProviderPendingAsync(job, "SMTP provider settings are incomplete", ct);
        }

        var inbox = await db.Set<NotificationInboxItem>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == job.NotificationInboxItemId && x.TenantId == job.TenantId, ct);
        if (inbox is null)
            return await MarkFailedAsync(job, "not-found", "Notification inbox item was not found", false, ct);

        try
        {
            using var message = new MailMessage(settings.FromAddress, job.RecipientAddress, inbox.Title, inbox.Body);
            using var client = new SmtpClient(settings.Host, settings.Port <= 0 ? 25 : settings.Port)
            {
                EnableSsl = settings.EnableSsl
            };

            if (!string.IsNullOrWhiteSpace(settings.UserName))
                client.Credentials = new NetworkCredential(settings.UserName, settings.Password);

            await client.SendMailAsync(message, ct);
            await MarkSentAsync(job, "smtp", ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SMTP delivery failed for notification delivery job {DeliveryJobId}", job.Id);
            return await MarkFailedAsync(job, "smtp-send-failed", ex.Message, true, ct);
        }
    }

    private async Task<Result> DispatchWebhookAsync(NotificationDeliveryJob job, CancellationToken ct)
    {
        var provider = await GetDefaultProviderAsync(job.TenantId, NotificationDeliveryChannel.Webhook, ct);
        if (provider?.SettingsJson is null)
            return await MarkProviderPendingAsync(job, "Webhook provider is not configured", ct);

        var settings = JsonSerializer.Deserialize<WebhookProviderSettings>(provider.SettingsJson);
        if (settings is null ||
            string.IsNullOrWhiteSpace(settings.Url) ||
            !Uri.TryCreate(settings.Url, UriKind.Absolute, out var uri))
        {
            return await MarkProviderPendingAsync(job, "Webhook provider settings are incomplete", ct);
        }

        var payload = job.PayloadJson ?? "{}";
        using var request = new HttpRequestMessage(HttpMethod.Post, uri)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        request.Headers.TryAddWithoutValidation("X-XFramework-Delivery-Id", job.Id.ToString());
        request.Headers.TryAddWithoutValidation("X-XFramework-Timestamp", timestamp);

        if (!string.IsNullOrWhiteSpace(settings.SigningSecret))
        {
            var signature = ComputeWebhookSignature(settings.SigningSecret, $"{timestamp}.{payload}");
            request.Headers.TryAddWithoutValidation("X-XFramework-Signature", signature);
        }

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 5, 60)) };
            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return await MarkFailedAsync(
                    job,
                    "webhook-non-success",
                    $"Webhook returned {(int)response.StatusCode}",
                    true,
                    ct);
            }

            await MarkSentAsync(job, $"webhook:{(int)response.StatusCode}", ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Webhook delivery failed for notification delivery job {DeliveryJobId}", job.Id);
            return await MarkFailedAsync(job, "webhook-send-failed", ex.Message, true, ct);
        }
    }

    private async Task<NotificationProviderSetting?> GetDefaultProviderAsync(
        Guid tenantId,
        NotificationDeliveryChannel channel,
        CancellationToken ct) =>
        await db.Set<NotificationProviderSetting>()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Channel == channel && !x.IsDeleted && x.IsEnabled)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

    private static string ComputeWebhookSignature(string secret, string payload)
    {
        var key = Encoding.UTF8.GetBytes(secret);
        var bytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(key);
        return $"sha256={Convert.ToHexString(hmac.ComputeHash(bytes)).ToLowerInvariant()}";
    }

    private async Task<Result> MarkSentAsync(NotificationDeliveryJob job, string providerMessageId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var tracked = await db.Set<NotificationDeliveryJob>()
            .AsTracking()
            .FirstAsync(x => x.Id == job.Id, ct);

        tracked.Status = NotificationDeliveryStatus.Sent;
        tracked.ProviderMessageId = providerMessageId;
        tracked.LeasedUntil = null;
        tracked.LeaseOwner = null;
        tracked.CompletedAt = now;
        tracked.ModifiedAt = now;

        await UpsertStatusProjectionAsync(tracked, NotificationDeliveryStatus.Sent, providerMessageId, null, null, now, ct);
        await CompleteLatestAttemptAsync(tracked, NotificationDeliveryStatus.Sent, providerMessageId, null, null, now, ct);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<Result> MarkFailedAsync(
        NotificationDeliveryJob job,
        string errorCode,
        string errorMessage,
        bool retryable,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var tracked = await db.Set<NotificationDeliveryJob>()
            .AsTracking()
            .FirstAsync(x => x.Id == job.Id, ct);

        tracked.LastErrorCode = errorCode;
        tracked.LastErrorMessage = errorMessage;
        tracked.LeasedUntil = null;
        tracked.LeaseOwner = null;

        if (retryable && tracked.AttemptCount < Math.Max(_maxAttempts, tracked.MaxAttempts))
        {
            tracked.Status = NotificationDeliveryStatus.Queued;
            tracked.NextAttemptAt = now.AddMinutes(Math.Min(60, Math.Pow(2, tracked.AttemptCount)));
        }
        else
        {
            tracked.Status = NotificationDeliveryStatus.Failed;
            tracked.CompletedAt = now;
        }

        tracked.ModifiedAt = now;

        await UpsertStatusProjectionAsync(tracked, tracked.Status, null, errorCode, errorMessage, now, ct);
        await CompleteLatestAttemptAsync(tracked, tracked.Status, null, errorCode, errorMessage, now, ct);
        await db.SaveChangesAsync(ct);
        return Result.Failure(errorMessage);
    }

    private async Task UpsertStatusProjectionAsync(
        NotificationDeliveryJob job,
        NotificationDeliveryStatus status,
        string? providerMessageId,
        string? errorCode,
        string? errorMessage,
        DateTime recordedAt,
        CancellationToken ct)
    {
        var projection = await db.Set<NotificationDeliveryStatusRecord>()
            .AsTracking()
            .FirstOrDefaultAsync(
                x => x.TenantId == job.TenantId &&
                     x.NotificationInboxItemId == job.NotificationInboxItemId &&
                     x.Channel == job.Channel,
                ct);

        if (projection is null)
        {
            projection = new NotificationDeliveryStatusRecord
            {
                Id = Guid.NewGuid(),
                TenantId = job.TenantId,
                NotificationInboxItemId = job.NotificationInboxItemId,
                Channel = job.Channel,
                CreatedAt = recordedAt,
                ConcurrencyStamp = Guid.NewGuid(),
                IsEnabled = true
            };
            db.Set<NotificationDeliveryStatusRecord>().Add(projection);
        }

        projection.Status = status;
        projection.ProviderMessageId = providerMessageId ?? projection.ProviderMessageId;
        projection.ErrorCode = errorCode;
        projection.ErrorMessage = errorMessage;
        projection.AttemptNumber = job.AttemptCount;
        projection.RecordedAt = recordedAt;
        projection.ModifiedAt = recordedAt;
    }

    private async Task CompleteLatestAttemptAsync(
        NotificationDeliveryJob job,
        NotificationDeliveryStatus status,
        string? providerMessageId,
        string? errorCode,
        string? errorMessage,
        DateTime completedAt,
        CancellationToken ct)
    {
        var attempt = await db.Set<NotificationDeliveryAttempt>()
            .AsTracking()
            .Where(x => x.NotificationDeliveryJobId == job.Id)
            .OrderByDescending(x => x.AttemptNumber)
            .FirstOrDefaultAsync(ct);

        if (attempt is null)
            return;

        attempt.Status = status;
        attempt.ProviderMessageId = providerMessageId;
        attempt.ErrorCode = errorCode;
        attempt.ErrorMessage = errorMessage;
        attempt.CompletedAt = completedAt;
        attempt.ModifiedAt = completedAt;
    }

    private sealed record SmtpProviderSettings(
        string Host,
        int Port,
        string FromAddress,
        bool EnableSsl = true,
        string? UserName = null,
        string? Password = null);

    private sealed record WebhookProviderSettings(
        string Url,
        string? SigningSecret = null,
        int TimeoutSeconds = 15);
}
