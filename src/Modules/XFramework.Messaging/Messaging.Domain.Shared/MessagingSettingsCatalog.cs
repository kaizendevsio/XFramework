namespace Messaging.Domain.Shared;

public enum MessagingSettingValueKind
{
    Boolean,
    Number,
    Text,
    Csv,
    Option,
    Template
}

public sealed record MessagingSettingDefinition(
    string SectionKey,
    string GroupName,
    string Key,
    string Label,
    string Description,
    string DefaultValue,
    string? Unit,
    bool MatchFirstConfigInGroup,
    Guid GroupSystemReferenceId,
    MessagingSettingValueKind ValueKind,
    IReadOnlyList<string>? Options = null);

public static class MessagingSettingsCatalog
{
    public const string ChatSectionKey = "chat";
    public const string PolicySectionKey = "policy";
    public const string TemplatesSectionKey = "templates";

    private static readonly Guid MessagingGroupSystemReferenceId =
        new("651f06b1-b70e-4822-a638-06ad07b5307c");

    private static readonly Guid OtpGroupSystemReferenceId =
        new("b5e1da3f-c4ad-4dc2-9187-34f5781adf01");

    private static readonly Guid PasswordResetGroupSystemReferenceId =
        new("8dbef62d-f510-48ed-b970-76b51b9a5119");

    public static readonly IReadOnlyList<MessagingSettingDefinition> Definitions =
    [
        new(ChatSectionKey, "Messaging.Chat", "DirectThreads.Enabled", "Direct Threads", "Controls whether tenant users can create 1:1 chat threads.", "true", "boolean", false, MessagingGroupSystemReferenceId, MessagingSettingValueKind.Boolean),
        new(ChatSectionKey, "Messaging.Chat", "GroupThreads.Enabled", "Group Threads", "Controls whether tenant users can create multi-member chat threads.", "true", "boolean", false, MessagingGroupSystemReferenceId, MessagingSettingValueKind.Boolean),
        new(ChatSectionKey, "Messaging.Chat", "GroupThreads.MaxMembers", "Group Member Limit", "Maximum members allowed in a group chat thread.", "250", "count", false, MessagingGroupSystemReferenceId, MessagingSettingValueKind.Number),
        new(ChatSectionKey, "Messaging.Chat", "Messages.EditWindowMinutes", "Message Edit Window", "Minutes after sending during which a message can be edited.", "15", "minutes", false, MessagingGroupSystemReferenceId, MessagingSettingValueKind.Number),
        new(ChatSectionKey, "Messaging.Chat", "Messages.DeleteMode", "Delete Mode", "Default delete behavior for user-created messages.", "soft-delete", "mode", false, MessagingGroupSystemReferenceId, MessagingSettingValueKind.Option, ["soft-delete", "delete-for-me", "disabled"]),
        new(ChatSectionKey, "Messaging.Chat", "ReadReceipts.Enabled", "Read Receipts", "Controls read-state delivery records for chat messages.", "true", "boolean", false, MessagingGroupSystemReferenceId, MessagingSettingValueKind.Boolean),
        new(ChatSectionKey, "Messaging.Chat", "TypingIndicators.Enabled", "Typing Indicators", "Controls transient typing events over Bolt.", "true", "boolean", false, MessagingGroupSystemReferenceId, MessagingSettingValueKind.Boolean),
        new(ChatSectionKey, "Messaging.Chat", "Presence.Enabled", "Presence", "Controls online/offline and last-active presence events.", "true", "boolean", false, MessagingGroupSystemReferenceId, MessagingSettingValueKind.Boolean),

        new(PolicySectionKey, "Messaging.Policy", "Attachments.MaxSizeBytes", "Attachment Size Limit", "Maximum linked StorageFile size accepted by Messaging.", "26214400", "bytes", false, MessagingGroupSystemReferenceId, MessagingSettingValueKind.Number),
        new(PolicySectionKey, "Messaging.Policy", "Attachments.AllowedContentFamilies", "Attachment Content Families", "Comma-separated content families accepted for message file links.", "image,video,audio,text,pdf,json,zip,vnd", "csv", false, MessagingGroupSystemReferenceId, MessagingSettingValueKind.Csv),
        new(PolicySectionKey, "Messaging.Policy", "Attachments.BlockedExtensions", "Blocked Attachment Extensions", "Comma-separated file extensions rejected by Messaging attachment validation.", ".bat,.cmd,.com,.dll,.exe,.js,.msi,.ps1,.scr,.sh,.vbs", "csv", false, MessagingGroupSystemReferenceId, MessagingSettingValueKind.Csv),
        new(PolicySectionKey, "Messaging.Policy", "RateLimits.MessageCreatePerMinute", "Message Create Rate", "Tenant policy target for message creation rate limiting.", "60", "per-minute", false, MessagingGroupSystemReferenceId, MessagingSettingValueKind.Number),
        new(PolicySectionKey, "Messaging.Policy", "RateLimits.InviteCreatePerMinute", "Invite Create Rate", "Tenant policy target for invite creation rate limiting.", "30", "per-minute", false, MessagingGroupSystemReferenceId, MessagingSettingValueKind.Number),
        new(PolicySectionKey, "Messaging.Policy", "RateLimits.ReactionCreatePerMinute", "Reaction Create Rate", "Tenant policy target for reaction creation rate limiting.", "120", "per-minute", false, MessagingGroupSystemReferenceId, MessagingSettingValueKind.Number),
        new(PolicySectionKey, "Messaging.Policy", "Retention.SoftDeletedMessageDays", "Soft Deleted Message Retention", "Days to retain soft-deleted messages for audit review.", "90", "days", false, MessagingGroupSystemReferenceId, MessagingSettingValueKind.Number),
        new(PolicySectionKey, "Messaging.Policy", "Moderation.AdminAuditVisible", "Admin Audit Visibility", "Controls whether moderation audit records are visible in ControlPanel.", "true", "boolean", false, MessagingGroupSystemReferenceId, MessagingSettingValueKind.Boolean),

        new(TemplatesSectionKey, "MessagingService_Otp", "MessageTemplate", "OTP Message Template", "Template used when Identity sends one-time passwords through Messaging. Use |Value| for the generated code.", "Your verification code is |Value|.", "template", true, OtpGroupSystemReferenceId, MessagingSettingValueKind.Template),
        new(TemplatesSectionKey, "MessagingService_PasswordReset", "MessageTemplate", "Password Reset Template", "Template used when Identity sends password reset tokens through Messaging. Use |Token| for the generated token.", "Your password reset token is: |Token|. This token expires in 30 minutes.", "template", true, PasswordResetGroupSystemReferenceId, MessagingSettingValueKind.Template),
        new(TemplatesSectionKey, "Messaging.Transport", "Settings:Messaging:Sms:AgentClusterId", "SMS Agent Cluster", "Agent cluster used when legacy direct SMS messages are sent through Messaging.", string.Empty, "guid", false, MessagingGroupSystemReferenceId, MessagingSettingValueKind.Text),
        new(TemplatesSectionKey, "Messaging.Transport", "DirectMessages.DefaultTransport", "Default Direct Message Transport", "Operational default for legacy direct-message diagnostics.", MessageTransportType.Sms.ToString(), "MessageTransportType", false, MessagingGroupSystemReferenceId, MessagingSettingValueKind.Option, Enum.GetNames<MessageTransportType>()),
        new(TemplatesSectionKey, "Messaging.Transport", "Notifications.FallbackTransport", "Notification Fallback Transport", "Fallback transport when in-app notification delivery is unavailable.", MessageTransportType.Push.ToString(), "MessageTransportType", false, MessagingGroupSystemReferenceId, MessagingSettingValueKind.Option, Enum.GetNames<MessageTransportType>())
    ];

    public static IReadOnlyList<MessagingSettingDefinition> GetSection(string sectionKey) =>
        Definitions
            .Where(x => string.Equals(x.SectionKey, sectionKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

    public static MessagingSettingDefinition? Find(string groupName, string key) =>
        Definitions.FirstOrDefault(x =>
            string.Equals(x.GroupName, groupName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
}
