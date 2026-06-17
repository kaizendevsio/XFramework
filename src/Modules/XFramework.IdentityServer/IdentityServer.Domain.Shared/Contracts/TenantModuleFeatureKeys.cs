namespace IdentityServer.Domain.Shared.Contracts;

public static class TenantModuleFeatureKeys
{
    public const string Wallets = "wallets";
    public const string Inventario = "inventario";
    public const string Messaging = "messaging";
    public const string MessagingChat = "messaging.chat";
    public const string MessagingAudioVideo = "messaging.audio_video";
    public const string Community = "community";
    public const string Payments = "payments";
    public const string Notifications = "notifications";

    public const string ChatSubFeature = "chat";
    public const string AudioVideoSubFeature = "audio_video";

    public static IReadOnlyList<TenantModuleFeatureDefinition> All { get; } =
    [
        new(Wallets, string.Empty, "Wallets", "Wallet accounts, balances, transfers, deposits, and withdrawals.", "wallet"),
        new(Inventario, string.Empty, "Inventario", "Product catalog and inventory operations.", "boxes"),
        new(Messaging, ChatSubFeature, "Messaging Chat", "Threads, direct messages, reactions, and attachments.", "message-circle"),
        new(Messaging, AudioVideoSubFeature, "Messaging Audio/Video", "Audio and video communication features.", "video"),
        new(Community, string.Empty, "Community", "Community identities, content, feed, and connections.", "users"),
        new(Payments, string.Empty, "Payments", "Payment gateway and cash-in/cash-out capabilities.", "credit-card"),
        new(Notifications, string.Empty, "Notifications", "Tenant notifications and read-state workflows.", "bell")
    ];

    public static (string ModuleKey, string SubFeatureKey) Normalize(string moduleKey, string? subFeatureKey = null)
    {
        var normalizedModuleKey = NormalizePart(moduleKey);
        var normalizedSubFeatureKey = NormalizePart(subFeatureKey);

        if (string.IsNullOrWhiteSpace(normalizedSubFeatureKey))
        {
            var separatorIndex = normalizedModuleKey.IndexOf('.', StringComparison.Ordinal);
            if (separatorIndex > 0 && separatorIndex < normalizedModuleKey.Length - 1)
            {
                normalizedSubFeatureKey = normalizedModuleKey[(separatorIndex + 1)..];
                normalizedModuleKey = normalizedModuleKey[..separatorIndex];
            }
        }

        return (normalizedModuleKey, normalizedSubFeatureKey);
    }

    public static string Combine(string moduleKey, string? subFeatureKey = null)
    {
        var (normalizedModuleKey, normalizedSubFeatureKey) = Normalize(moduleKey, subFeatureKey);
        return string.IsNullOrWhiteSpace(normalizedSubFeatureKey)
            ? normalizedModuleKey
            : $"{normalizedModuleKey}.{normalizedSubFeatureKey}";
    }

    public static TenantModuleFeatureDefinition? Find(string moduleKey, string? subFeatureKey = null)
    {
        var key = Combine(moduleKey, subFeatureKey);
        return All.FirstOrDefault(definition =>
            string.Equals(definition.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePart(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
}
