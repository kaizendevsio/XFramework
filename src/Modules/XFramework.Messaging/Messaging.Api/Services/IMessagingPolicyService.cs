using Messaging.Domain.Shared;
using Microsoft.Extensions.Caching.Memory;
using XFramework.Domain.Shared.DataContext;

namespace Messaging.Api.Services;

public sealed record MessagingPolicySnapshot(
    bool DirectThreadsEnabled,
    bool GroupThreadsEnabled,
    int GroupMaxMembers,
    int MessageEditWindowMinutes,
    string DeleteMode,
    bool ReadReceiptsEnabled,
    bool TypingIndicatorsEnabled,
    bool PresenceEnabled,
    long AttachmentMaxSizeBytes,
    IReadOnlySet<string> AttachmentAllowedContentFamilies,
    IReadOnlySet<string> AttachmentBlockedExtensions,
    int MessageCreatePerMinute,
    int InviteCreatePerMinute,
    int ReactionCreatePerMinute,
    int AttachmentLinkPerMinute,
    int ReportCreatePerMinute,
    int DirectExternalTransportPerMinute,
    int SoftDeletedMessageRetentionDays,
    bool ModerationAdminAuditVisible);

public interface IMessagingPolicyService
{
    Task<MessagingPolicySnapshot> GetPolicyAsync(Guid tenantId, CancellationToken ct = default);
    void Invalidate(Guid tenantId);
}

public sealed class MessagingPolicyService(
    IDataContext dataContext,
    IMemoryCache cache) : IMessagingPolicyService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async Task<MessagingPolicySnapshot> GetPolicyAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));

        var cacheKey = CacheKey(tenantId);
        if (cache.TryGetValue(cacheKey, out MessagingPolicySnapshot? policy) && policy is not null)
            return policy;

        var stored = await dataContext.Query<RegistryConfiguration>()
            .Where(x => x.TenantId == tenantId)
            .Where(x => !x.IsDeleted && x.IsEnabled)
            .ToListAsync(ct);

        var values = stored
            .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ModifiedAt ?? x.CreatedAt).First().Value, StringComparer.OrdinalIgnoreCase);

        policy = new MessagingPolicySnapshot(
            DirectThreadsEnabled: GetBoolean(values, "DirectThreads.Enabled"),
            GroupThreadsEnabled: GetBoolean(values, "GroupThreads.Enabled"),
            GroupMaxMembers: Math.Clamp(GetNumber(values, "GroupThreads.MaxMembers"), 2, 10000),
            MessageEditWindowMinutes: Math.Clamp(GetNumber(values, "Messages.EditWindowMinutes"), 0, 10080),
            DeleteMode: GetValue(values, "Messages.DeleteMode"),
            ReadReceiptsEnabled: GetBoolean(values, "ReadReceipts.Enabled"),
            TypingIndicatorsEnabled: GetBoolean(values, "TypingIndicators.Enabled"),
            PresenceEnabled: GetBoolean(values, "Presence.Enabled"),
            AttachmentMaxSizeBytes: Math.Max(0, GetLong(values, "Attachments.MaxSizeBytes")),
            AttachmentAllowedContentFamilies: GetCsv(values, "Attachments.AllowedContentFamilies"),
            AttachmentBlockedExtensions: GetCsv(values, "Attachments.BlockedExtensions"),
            MessageCreatePerMinute: Math.Max(0, GetNumber(values, "RateLimits.MessageCreatePerMinute")),
            InviteCreatePerMinute: Math.Max(0, GetNumber(values, "RateLimits.InviteCreatePerMinute")),
            ReactionCreatePerMinute: Math.Max(0, GetNumber(values, "RateLimits.ReactionCreatePerMinute")),
            AttachmentLinkPerMinute: Math.Max(0, GetNumber(values, "RateLimits.AttachmentLinkPerMinute")),
            ReportCreatePerMinute: Math.Max(0, GetNumber(values, "RateLimits.ReportCreatePerMinute")),
            DirectExternalTransportPerMinute: Math.Max(0, GetNumber(values, "RateLimits.DirectExternalTransportPerMinute")),
            SoftDeletedMessageRetentionDays: Math.Max(0, GetNumber(values, "Retention.SoftDeletedMessageDays")),
            ModerationAdminAuditVisible: GetBoolean(values, "Moderation.AdminAuditVisible"));

        cache.Set(cacheKey, policy, CacheDuration);
        return policy;
    }

    public void Invalidate(Guid tenantId)
    {
        if (tenantId != Guid.Empty)
            cache.Remove(CacheKey(tenantId));
    }

    private static string CacheKey(Guid tenantId) => $"messaging:policy:{tenantId:N}";

    private static bool GetBoolean(IReadOnlyDictionary<string, string?> values, string key) =>
        bool.TryParse(GetValue(values, key), out var value) && value;

    private static int GetNumber(IReadOnlyDictionary<string, string?> values, string key) =>
        int.TryParse(GetValue(values, key), out var value) ? value : 0;

    private static long GetLong(IReadOnlyDictionary<string, string?> values, string key) =>
        long.TryParse(GetValue(values, key), out var value) ? value : 0;

    private static IReadOnlySet<string> GetCsv(IReadOnlyDictionary<string, string?> values, string key) =>
        GetValue(values, key)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static x => x.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string GetValue(IReadOnlyDictionary<string, string?> values, string key)
    {
        if (values.TryGetValue(key, out var stored) && !string.IsNullOrWhiteSpace(stored))
            return stored.Trim();

        return MessagingSettingsCatalog.Definitions
            .First(definition => string.Equals(definition.Key, key, StringComparison.OrdinalIgnoreCase))
            .DefaultValue;
    }
}
