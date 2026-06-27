namespace Communications.Domain.Shared;

public enum CommunicationsSettingValueKind
{
    Boolean,
    Number,
    Text,
    Csv,
    Option,
    Template
}

public sealed record CommunicationsSettingDefinition(
    string SectionKey,
    string GroupName,
    string Key,
    string Label,
    string Description,
    string DefaultValue,
    string? Unit,
    bool MatchFirstConfigInGroup,
    Guid GroupSystemReferenceId,
    CommunicationsSettingValueKind ValueKind,
    IReadOnlyList<string>? Options = null);

public static class CommunicationsSettingsCatalog
{
    public const string ChatSectionKey = "chat";
    public const string PolicySectionKey = "policy";
    public const string TemplatesSectionKey = "templates";

    private static readonly Guid CommunicationsGroupSystemReferenceId =
        new("651f06b1-b70e-4822-a638-06ad07b5307c");

    private static readonly Guid OtpGroupSystemReferenceId =
        new("b5e1da3f-c4ad-4dc2-9187-34f5781adf01");

    private static readonly Guid PasswordResetGroupSystemReferenceId =
        new("8dbef62d-f510-48ed-b970-76b51b9a5119");

    public static readonly IReadOnlyList<CommunicationsSettingDefinition> Definitions =
    [
        new(ChatSectionKey, "Communications.Chat", "DirectThreads.Enabled", "Direct Threads", "Controls whether tenant users can create 1:1 chat threads.", "true", "boolean", false, CommunicationsGroupSystemReferenceId, CommunicationsSettingValueKind.Boolean),
        new(ChatSectionKey, "Communications.Chat", "GroupThreads.Enabled", "Group Threads", "Controls whether tenant users can create multi-member chat threads.", "true", "boolean", false, CommunicationsGroupSystemReferenceId, CommunicationsSettingValueKind.Boolean),
        new(ChatSectionKey, "Communications.Chat", "GroupThreads.MaxMembers", "Group Member Limit", "Maximum members allowed in a group chat thread.", "250", "count", false, CommunicationsGroupSystemReferenceId, CommunicationsSettingValueKind.Number),
        new(ChatSectionKey, "Communications.Chat", "Messages.EditWindowMinutes", "Message Edit Window", "Minutes after sending during which a message can be edited.", "15", "minutes", false, CommunicationsGroupSystemReferenceId, CommunicationsSettingValueKind.Number),
        new(ChatSectionKey, "Communications.Chat", "Messages.DeleteMode", "Delete Mode", "Default delete behavior for user-created messages.", "soft-delete", "mode", false, CommunicationsGroupSystemReferenceId, CommunicationsSettingValueKind.Option, ["soft-delete", "delete-for-me", "disabled"]),
        new(ChatSectionKey, "Communications.Chat", "ReadReceipts.Enabled", "Read Receipts", "Controls read-state delivery records for chat messages.", "true", "boolean", false, CommunicationsGroupSystemReferenceId, CommunicationsSettingValueKind.Boolean),
        new(ChatSectionKey, "Communications.Chat", "TypingIndicators.Enabled", "Typing Indicators", "Controls transient typing events over Bolt.", "true", "boolean", false, CommunicationsGroupSystemReferenceId, CommunicationsSettingValueKind.Boolean),
        new(ChatSectionKey, "Communications.Chat", "Presence.Enabled", "Presence", "Controls online/offline and last-active presence events.", "true", "boolean", false, CommunicationsGroupSystemReferenceId, CommunicationsSettingValueKind.Boolean),

        new(PolicySectionKey, "Communications.Policy", "Attachments.MaxSizeBytes", "Attachment Size Limit", "Maximum linked StorageFile size accepted by Communications.", "26214400", "bytes", false, CommunicationsGroupSystemReferenceId, CommunicationsSettingValueKind.Number),
        new(PolicySectionKey, "Communications.Policy", "Attachments.AllowedContentFamilies", "Attachment Content Families", "Comma-separated content families accepted for message file links.", "image,video,audio,text,pdf,json,zip,vnd", "csv", false, CommunicationsGroupSystemReferenceId, CommunicationsSettingValueKind.Csv),
        new(PolicySectionKey, "Communications.Policy", "Attachments.BlockedExtensions", "Blocked Attachment Extensions", "Comma-separated file extensions rejected by Communications attachment validation.", ".bat,.cmd,.com,.dll,.exe,.js,.msi,.ps1,.scr,.sh,.vbs", "csv", false, CommunicationsGroupSystemReferenceId, CommunicationsSettingValueKind.Csv),
        new(PolicySectionKey, "Communications.Policy", "RateLimits.MessageCreatePerMinute", "Message Create Rate", "Tenant policy target for message creation rate limiting.", "60", "per-minute", false, CommunicationsGroupSystemReferenceId, CommunicationsSettingValueKind.Number),
        new(PolicySectionKey, "Communications.Policy", "RateLimits.InviteCreatePerMinute", "Invite Create Rate", "Tenant policy target for invite creation rate limiting.", "30", "per-minute", false, CommunicationsGroupSystemReferenceId, CommunicationsSettingValueKind.Number),
        new(PolicySectionKey, "Communications.Policy", "RateLimits.ReactionCreatePerMinute", "Reaction Create Rate", "Tenant policy target for reaction creation rate limiting.", "120", "per-minute", false, CommunicationsGroupSystemReferenceId, CommunicationsSettingValueKind.Number),
        new(PolicySectionKey, "Communications.Policy", "RateLimits.AttachmentLinkPerMinute", "Attachment Link Rate", "Tenant policy target for message attachment link actions.", "30", "per-minute", false, CommunicationsGroupSystemReferenceId, CommunicationsSettingValueKind.Number),
        new(PolicySectionKey, "Communications.Policy", "RateLimits.ReportCreatePerMinute", "Report Create Rate", "Tenant policy target for moderation report creation.", "10", "per-minute", false, CommunicationsGroupSystemReferenceId, CommunicationsSettingValueKind.Number),
        new(PolicySectionKey, "Communications.Policy", "RateLimits.DirectExternalTransportPerMinute", "External Direct Transport Rate", "Tenant policy target for direct external transport requests.", "20", "per-minute", false, CommunicationsGroupSystemReferenceId, CommunicationsSettingValueKind.Number),
        new(PolicySectionKey, "Communications.Policy", "Retention.SoftDeletedMessageDays", "Soft Deleted Message Retention", "Days to retain soft-deleted messages for audit review.", "90", "days", false, CommunicationsGroupSystemReferenceId, CommunicationsSettingValueKind.Number),
        new(PolicySectionKey, "Communications.Policy", "Moderation.AdminAuditVisible", "Admin Audit Visibility", "Controls whether moderation audit records are visible in ControlPanel.", "true", "boolean", false, CommunicationsGroupSystemReferenceId, CommunicationsSettingValueKind.Boolean),

        new(TemplatesSectionKey, "CommunicationsService_Otp", "MessageTemplate", "OTP Message Template", "Template used when Identity sends one-time passwords through Communications. Use |Value| for the generated code.", "Your verification code is |Value|.", "template", true, OtpGroupSystemReferenceId, CommunicationsSettingValueKind.Template),
        new(TemplatesSectionKey, "CommunicationsService_PasswordReset", "MessageTemplate", "Password Reset Template", "Template used when Identity sends password reset tokens through Communications. Use |Token| for the generated token.", "Your password reset token is: |Token|. This token expires in 30 minutes.", "template", true, PasswordResetGroupSystemReferenceId, CommunicationsSettingValueKind.Template),
        new(TemplatesSectionKey, "Communications.Transport", "Settings:Communications:Sms:AgentClusterId", "SMS Agent Cluster", "Agent cluster used when legacy direct SMS messages are sent through Communications.", string.Empty, "guid", false, CommunicationsGroupSystemReferenceId, CommunicationsSettingValueKind.Text),
        new(TemplatesSectionKey, "Communications.Transport", "DirectMessages.DefaultTransport", "Default Direct Message Transport", "Operational default for legacy direct-message diagnostics.", MessageTransportType.Sms.ToString(), "MessageTransportType", false, CommunicationsGroupSystemReferenceId, CommunicationsSettingValueKind.Option, Enum.GetNames<MessageTransportType>()),
        new(TemplatesSectionKey, "Communications.Transport", "Notifications.FallbackTransport", "Notification Fallback Transport", "Fallback transport when in-app notification delivery is unavailable.", MessageTransportType.Push.ToString(), "MessageTransportType", false, CommunicationsGroupSystemReferenceId, CommunicationsSettingValueKind.Option, Enum.GetNames<MessageTransportType>())
    ];

    public static IReadOnlyList<CommunicationsSettingDefinition> GetSection(string sectionKey) =>
        Definitions
            .Where(x => string.Equals(x.SectionKey, sectionKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

    public static CommunicationsSettingDefinition? Find(string groupName, string key) =>
        Definitions.FirstOrDefault(x =>
            string.Equals(x.GroupName, groupName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
}
