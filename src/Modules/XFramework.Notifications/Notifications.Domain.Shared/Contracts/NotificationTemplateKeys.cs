namespace Notifications.Domain.Shared.Contracts;

public static class NotificationTemplateKeys
{
    public const string SystemGeneric = "notifications.system.generic";
    public const string MessageReceived = "notifications.message.received";
    public const string CommunityMention = "notifications.community.mention";
    public const string CommunityReaction = "notifications.community.reaction";
    public const string WalletTransaction = "notifications.wallet.transaction";
    public const string VerificationCode = "notifications.identity.verification-code";

    public static readonly IReadOnlySet<string> KnownKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        SystemGeneric,
        MessageReceived,
        CommunityMention,
        CommunityReaction,
        WalletTransaction,
        VerificationCode
    };
}
