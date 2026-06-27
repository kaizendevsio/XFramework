using System.Text.Json;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.BusinessObjects;

namespace Notifications.Api.Services;

public sealed class NotificationService(AppDbContext db, ILogger<NotificationService> logger)
{
    private const char TemplateKeySeparator = '\n';

    public async Task<Result<NotificationInboxItemResponse>> CreateNotificationAsync(
        CreateNotificationRequest request,
        CancellationToken ct)
    {
        if (!TryResolveTenantId(request.TenantId, request.Metadata, out var tenantId))
            return Result<NotificationInboxItemResponse>.Failure("Tenant ID is required", 400);

        if (!string.IsNullOrWhiteSpace(request.CorrelationId))
        {
            var normalizedCorrelationId = request.CorrelationId.Trim();
            var existing = await db.Set<NotificationInboxItem>()
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId)
                .Where(item => item.CorrelationId == normalizedCorrelationId)
                .Where(item => !item.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (existing is not null)
                return Result<NotificationInboxItemResponse>.Success(ToInboxResponse(existing), 200, "Notification already exists");
        }

        var preferences = await GetPreferenceEntityAsync(tenantId, request.RecipientCredentialId, ct);
        var enabledChannels = preferences?.EnabledChannels ?? NotificationPreferenceDefaults.EnabledChannels;
        var requestedChannels = NotificationPreferenceDefaults.Normalize(request.DeliveryChannels);
        var effectiveChannels = requestedChannels & enabledChannels;

        if (effectiveChannels == NotificationDeliveryChannel.None)
        {
            logger.LogInformation(
                "Notification suppressed because no requested channels are enabled. TenantId={TenantId} RecipientCredentialId={CredentialId} TemplateKey={TemplateKey}",
                tenantId,
                request.RecipientCredentialId,
                request.TemplateKey);
            return Result<NotificationInboxItemResponse>.Conflict("No enabled delivery channels are available for this recipient");
        }

        if (IsTemplateDisabled(preferences, request.TemplateKey))
            return Result<NotificationInboxItemResponse>.Conflict("Notification template is disabled for this recipient");

        var item = new NotificationInboxItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            RecipientCredentialId = request.RecipientCredentialId,
            SourceCredentialId = request.SourceCredentialId,
            TemplateKey = request.TemplateKey.Trim(),
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            DeliveryChannels = effectiveChannels,
            CorrelationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? null : request.CorrelationId.Trim(),
            DataJson = request.Data is null ? null : JsonSerializer.Serialize(request.Data),
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid(),
            IsEnabled = true
        };

        db.Set<NotificationInboxItem>().Add(item);
        AddDeliveryRows(item, effectiveChannels, request.DeliveryAddress);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (!string.IsNullOrWhiteSpace(item.CorrelationId))
        {
            db.Entry(item).State = EntityState.Detached;
            var existing = await db.Set<NotificationInboxItem>()
                .AsNoTracking()
                .Where(existingItem => existingItem.TenantId == tenantId)
                .Where(existingItem => existingItem.CorrelationId == item.CorrelationId)
                .Where(existingItem => !existingItem.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (existing is not null)
                return Result<NotificationInboxItemResponse>.Success(ToInboxResponse(existing), 200, "Notification already exists");

            throw;
        }

        logger.LogInformation(
            "Notification {NotificationId} created for credential {CredentialId} in tenant {TenantId}",
            item.Id,
            item.RecipientCredentialId,
            tenantId);

        return Result<NotificationInboxItemResponse>.Success(ToInboxResponse(item), 201, "Notification created");
    }

    public async Task<Result<GetNotificationInboxResponse>> GetInboxAsync(
        GetNotificationInboxRequest request,
        CancellationToken ct)
    {
        if (!TryResolveTenantId(request.TenantId, request.Metadata, out var tenantId))
            return Result<GetNotificationInboxResponse>.Failure("Tenant ID is required", 400);

        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 20 : Math.Min(request.PageSize, 100);

        var query = db.Set<NotificationInboxItem>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.RecipientCredentialId == request.RecipientCredentialId);

        if (request.IsRead.HasValue)
            query = query.Where(item => item.IsRead == request.IsRead.Value);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(item => item.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new NotificationInboxItemResponse
            {
                Id = item.Id,
                TenantId = item.TenantId,
                RecipientCredentialId = item.RecipientCredentialId,
                SourceCredentialId = item.SourceCredentialId,
                TemplateKey = item.TemplateKey,
                Title = item.Title,
                Body = item.Body,
                DeliveryChannels = item.DeliveryChannels,
                CorrelationId = item.CorrelationId,
                DataJson = item.DataJson,
                IsRead = item.IsRead,
                ReadAt = item.ReadAt,
                CreatedAt = item.CreatedAt
            })
            .ToListAsync(ct);

        var response = new GetNotificationInboxResponse
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize)
        };

        return Result<GetNotificationInboxResponse>.Success(response);
    }

    public async Task<Result> MarkReadAsync(MarkNotificationReadRequest request, CancellationToken ct)
    {
        if (!TryResolveTenantId(request.TenantId, request.Metadata, out var tenantId))
            return Result.Failure("Tenant ID is required", 400);

        var items = await db.Set<NotificationInboxItem>()
            .AsTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.RecipientCredentialId == request.RecipientCredentialId &&
                request.NotificationIds.Contains(item.Id))
            .ToListAsync(ct);

        if (items.Count == 0)
            return Result.NotFound("No matching notifications were found");

        var readAt = DateTime.UtcNow;
        foreach (var item in items.Where(item => !item.IsRead))
        {
            item.IsRead = true;
            item.ReadAt = readAt;
            item.ModifiedAt = readAt;
        }

        await db.SaveChangesAsync(ct);

        return Result.Success($"Marked {items.Count} notification(s) as read");
    }

    public async Task<Result<NotificationPreferencesResponse>> GetPreferencesAsync(
        Guid tenantId,
        Guid credentialId,
        CancellationToken ct)
    {
        if (tenantId == Guid.Empty)
            return Result<NotificationPreferencesResponse>.Failure("Tenant ID is required", 400);

        var preferences = await GetPreferenceEntityAsync(tenantId, credentialId, ct);
        return Result<NotificationPreferencesResponse>.Success(ToPreferencesResponse(tenantId, credentialId, preferences));
    }

    public async Task<Result<NotificationPreferencesResponse>> UpdatePreferencesAsync(
        UpdateNotificationPreferencesRequest request,
        CancellationToken ct)
    {
        if (!TryResolveTenantId(request.TenantId, request.Metadata, out var tenantId))
            return Result<NotificationPreferencesResponse>.Failure("Tenant ID is required", 400);

        var normalizedTemplateKeys = request.DisabledTemplateKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var preferences = await db.Set<NotificationPreference>()
            .AsTracking()
            .FirstOrDefaultAsync(
                pref => pref.TenantId == tenantId && pref.CredentialId == request.CredentialId,
                ct);

        if (preferences is null)
        {
            preferences = new NotificationPreference
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CredentialId = request.CredentialId,
                CreatedAt = DateTime.UtcNow,
                ConcurrencyStamp = Guid.NewGuid(),
                IsEnabled = true
            };
            db.Set<NotificationPreference>().Add(preferences);
        }

        preferences.EnabledChannels = NotificationPreferenceDefaults.Normalize(request.EnabledChannels);
        preferences.DisabledTemplateKeys = SerializeTemplateKeys(normalizedTemplateKeys);
        preferences.DigestEnabled = request.DigestEnabled;
        preferences.ModifiedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Result<NotificationPreferencesResponse>.Success(
            ToPreferencesResponse(tenantId, request.CredentialId, preferences),
            "Notification preferences updated");
    }

    public async Task<Result<NotificationDeliveryStatusResponse>> RecordDeliveryStatusAsync(
        RecordNotificationDeliveryStatusRequest request,
        CancellationToken ct)
    {
        if (!TryResolveTenantId(request.TenantId, request.Metadata, out var tenantId))
            return Result<NotificationDeliveryStatusResponse>.Failure("Tenant ID is required", 400);

        var notificationExists = await db.Set<NotificationInboxItem>()
            .AsNoTracking()
            .AnyAsync(
                item => item.TenantId == tenantId && item.Id == request.NotificationInboxItemId,
                ct);

        if (!notificationExists)
            return Result<NotificationDeliveryStatusResponse>.NotFound("Notification was not found");

        var record = await db.Set<NotificationDeliveryStatusRecord>()
            .AsTracking()
            .FirstOrDefaultAsync(
                status =>
                    status.TenantId == tenantId &&
                    status.NotificationInboxItemId == request.NotificationInboxItemId &&
                    status.Channel == request.Channel,
                ct);

        if (record is not null && !CanTransition(record.Status, request.Status))
        {
            return Result<NotificationDeliveryStatusResponse>.Conflict(
                $"Cannot transition delivery status from {record.Status} to {request.Status}");
        }

        var recordedAt = request.RecordedAt ?? DateTime.UtcNow;
        if (record is null)
        {
            record = new NotificationDeliveryStatusRecord
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                NotificationInboxItemId = request.NotificationInboxItemId,
                Channel = request.Channel,
                CreatedAt = recordedAt,
                ConcurrencyStamp = Guid.NewGuid(),
                IsEnabled = true
            };
            db.Set<NotificationDeliveryStatusRecord>().Add(record);
        }

        record.Status = request.Status;
        record.ProviderMessageId = string.IsNullOrWhiteSpace(request.ProviderMessageId)
            ? record.ProviderMessageId
            : request.ProviderMessageId.Trim();
        record.ErrorCode = string.IsNullOrWhiteSpace(request.ErrorCode) ? null : request.ErrorCode.Trim();
        record.ErrorMessage = string.IsNullOrWhiteSpace(request.ErrorMessage) ? null : request.ErrorMessage.Trim();
        record.AttemptNumber = request.AttemptNumber <= 0 ? record.AttemptNumber : request.AttemptNumber;
        record.RecordedAt = recordedAt;
        record.ModifiedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Result<NotificationDeliveryStatusResponse>.Success(
            ToDeliveryStatusResponse(record),
            "Notification delivery status recorded");
    }

    private async Task<NotificationPreference?> GetPreferenceEntityAsync(
        Guid tenantId,
        Guid credentialId,
        CancellationToken ct) =>
        await db.Set<NotificationPreference>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                pref => pref.TenantId == tenantId && pref.CredentialId == credentialId,
                ct);

    private static bool TryResolveTenantId(Guid? requestTenantId, RequestMetadata metadata, out Guid tenantId)
    {
        tenantId = requestTenantId ?? metadata.TenantId ?? Guid.Empty;
        return tenantId != Guid.Empty;
    }

    private static bool IsTemplateDisabled(NotificationPreference? preference, string templateKey)
    {
        if (preference?.DisabledTemplateKeys is null)
            return false;

        return DeserializeTemplateKeys(preference.DisabledTemplateKeys)
            .Contains(templateKey.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    private static string? SerializeTemplateKeys(IReadOnlyCollection<string> keys) =>
        keys.Count == 0 ? null : string.Join(TemplateKeySeparator, keys);

    private static List<string> DeserializeTemplateKeys(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(TemplateKeySeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();

    private static NotificationInboxItemResponse ToInboxResponse(NotificationInboxItem item) => new()
    {
        Id = item.Id,
        TenantId = item.TenantId,
        RecipientCredentialId = item.RecipientCredentialId,
        SourceCredentialId = item.SourceCredentialId,
        TemplateKey = item.TemplateKey,
        Title = item.Title,
        Body = item.Body,
        DeliveryChannels = item.DeliveryChannels,
        CorrelationId = item.CorrelationId,
        DataJson = item.DataJson,
        IsRead = item.IsRead,
        ReadAt = item.ReadAt,
        CreatedAt = item.CreatedAt
    };

    private static NotificationPreferencesResponse ToPreferencesResponse(
        Guid tenantId,
        Guid credentialId,
        NotificationPreference? preference) => new()
    {
        Id = preference?.Id,
        TenantId = tenantId,
        CredentialId = credentialId,
        EnabledChannels = preference?.EnabledChannels ?? NotificationPreferenceDefaults.EnabledChannels,
        DisabledTemplateKeys = DeserializeTemplateKeys(preference?.DisabledTemplateKeys),
        DigestEnabled = preference?.DigestEnabled ?? false,
        IsDefault = preference is null
    };

    private static NotificationDeliveryStatusResponse ToDeliveryStatusResponse(NotificationDeliveryStatusRecord status) =>
        new()
        {
            Id = status.Id,
            TenantId = status.TenantId,
            NotificationInboxItemId = status.NotificationInboxItemId,
            Channel = status.Channel,
            Status = status.Status,
            ProviderMessageId = status.ProviderMessageId,
            ErrorCode = status.ErrorCode,
            ErrorMessage = status.ErrorMessage,
            AttemptNumber = status.AttemptNumber,
            RecordedAt = status.RecordedAt
        };

    private void AddDeliveryRows(
        NotificationInboxItem item,
        NotificationDeliveryChannel effectiveChannels,
        string? deliveryAddress)
    {
        var now = DateTime.UtcNow;
        foreach (var channel in EnumerateExternalChannels(effectiveChannels))
        {
            var correlationId = string.IsNullOrWhiteSpace(item.CorrelationId)
                ? $"notification:{item.Id:N}:{channel}"
                : $"{item.CorrelationId}:{channel}";

            db.Set<NotificationDeliveryStatusRecord>().Add(new NotificationDeliveryStatusRecord
            {
                Id = Guid.NewGuid(),
                TenantId = item.TenantId,
                NotificationInboxItemId = item.Id,
                Channel = channel,
                Status = NotificationDeliveryStatus.Queued,
                AttemptNumber = 0,
                RecordedAt = now,
                CreatedAt = now,
                ModifiedAt = now,
                ConcurrencyStamp = Guid.NewGuid(),
                IsEnabled = true
            });

            db.Set<NotificationDeliveryJob>().Add(new NotificationDeliveryJob
            {
                Id = Guid.NewGuid(),
                TenantId = item.TenantId,
                NotificationInboxItemId = item.Id,
                Channel = channel,
                Status = NotificationDeliveryStatus.Queued,
                ProviderKey = ResolveDefaultProviderKey(channel),
                RecipientAddress = string.IsNullOrWhiteSpace(deliveryAddress) ? null : deliveryAddress.Trim(),
                PayloadJson = JsonSerializer.Serialize(new
                {
                    item.Title,
                    item.Body,
                    item.TemplateKey,
                    item.DataJson
                }),
                CorrelationId = correlationId,
                NextAttemptAt = now,
                CreatedAt = now,
                ModifiedAt = now,
                ConcurrencyStamp = Guid.NewGuid(),
                IsEnabled = true
            });
        }
    }

    private static IEnumerable<NotificationDeliveryChannel> EnumerateExternalChannels(NotificationDeliveryChannel channels)
    {
        if (channels.HasFlag(NotificationDeliveryChannel.Email))
            yield return NotificationDeliveryChannel.Email;
        if (channels.HasFlag(NotificationDeliveryChannel.Sms))
            yield return NotificationDeliveryChannel.Sms;
        if (channels.HasFlag(NotificationDeliveryChannel.Webhook))
            yield return NotificationDeliveryChannel.Webhook;
    }

    private static string ResolveDefaultProviderKey(NotificationDeliveryChannel channel) =>
        channel switch
        {
            NotificationDeliveryChannel.Email => "smtp",
            NotificationDeliveryChannel.Sms => "sms-gateway",
            NotificationDeliveryChannel.Webhook => "webhook",
            _ => channel.ToString().ToLowerInvariant()
        };

    private static bool CanTransition(NotificationDeliveryStatus current, NotificationDeliveryStatus next) =>
        current == next ||
        current switch
        {
            NotificationDeliveryStatus.Pending => next is
                NotificationDeliveryStatus.Queued or
                NotificationDeliveryStatus.Sent or
                NotificationDeliveryStatus.Failed or
                NotificationDeliveryStatus.Suppressed or
                NotificationDeliveryStatus.Cancelled,
            NotificationDeliveryStatus.Queued => next is
                NotificationDeliveryStatus.Sent or
                NotificationDeliveryStatus.Failed or
                NotificationDeliveryStatus.Cancelled,
            NotificationDeliveryStatus.Sent => next is
                NotificationDeliveryStatus.Delivered or
                NotificationDeliveryStatus.Failed,
            NotificationDeliveryStatus.Delivered => false,
            NotificationDeliveryStatus.Failed => next is NotificationDeliveryStatus.Queued,
            NotificationDeliveryStatus.Suppressed => false,
            NotificationDeliveryStatus.Cancelled => false,
            _ => false
        };
}
