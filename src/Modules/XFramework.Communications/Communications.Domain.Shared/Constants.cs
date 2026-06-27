namespace Communications.Domain.Shared;

public static class MessageTypes
{
    public static readonly Guid Sms = new("f4fca110-790d-41d7-a0be-b5c699c9a9db");
    public static readonly Guid Email = new("67ee519a-babd-470a-88c5-cfcc578778ee");
    public static readonly Guid Push = new("073a033f-2c2d-4d18-8e27-85393e2a3592");
    public static readonly Guid Chat = new("d739a70a-dcf5-4707-b0a6-a8d1d39a42bf");
}

public static class MessageIntents
{
    public static readonly string Direct = nameof(Direct);
    public static readonly string Verification = nameof(Verification);
    public static readonly string Notification = nameof(Notification);
}

public static class MessageTemplateTypes
{
    public const string System = nameof(System);
    public const string Tenant = nameof(Tenant);
    public const string User = nameof(User);

    public static readonly string[] All = [System, Tenant, User];
}

public static class MessageTemplateKeys
{
    public const string IdentityOtp = "identity.otp";
    public const string IdentityPasswordReset = "identity.password-reset";
    public const string CommunicationsGeneric = "communications.generic";
}

public sealed record SystemMessageTemplateDefinition(
    Guid SystemReferenceId,
    string Key,
    string Name,
    string Description,
    string? Subject,
    string Body,
    IReadOnlyList<string> RequiredVariables);

public static class CommunicationsTemplateCatalog
{
    public static readonly IReadOnlyList<SystemMessageTemplateDefinition> SystemTemplates =
    [
        new(
            new Guid("2d876ee4-a3a5-4e38-a18f-45c76c3c7801"),
            MessageTemplateKeys.IdentityOtp,
            "Identity OTP",
            "Built-in one-time password template used by Identity verification messages.",
            null,
            "Your verification code is |Value|.",
            ["Value"]),
        new(
            new Guid("8df726dc-f12c-4ce6-bdf1-52ef3169223b"),
            MessageTemplateKeys.IdentityPasswordReset,
            "Identity Password Reset",
            "Built-in password reset template used by Identity recovery flows.",
            "Password reset",
            "Your password reset token is: |Token|. This token expires in 30 minutes.",
            ["Token"]),
        new(
            new Guid("7ce67e0f-e285-4b4c-a35a-699c0c04ab6a"),
            MessageTemplateKeys.CommunicationsGeneric,
            "Generic Message",
            "Built-in generic Communications template for tenant application messages.",
            null,
            "|Message|",
            ["Message"])
    ];

    public static SystemMessageTemplateDefinition? FindSystemTemplate(string key) =>
        SystemTemplates.FirstOrDefault(template =>
            string.Equals(template.Key, key, StringComparison.OrdinalIgnoreCase));
}

public static class MessageEvents
{
    public static readonly string SmsReceived = nameof(SmsReceived);
    public static readonly string EmailReceived = nameof(EmailReceived);
    public static readonly string PushReceived = nameof(PushReceived);
    public static readonly string ChatReceived = nameof(ChatReceived);
}

public static class MessageRealtimeEvents
{
    public static readonly string ThreadCreated = nameof(ThreadCreated);
    public static readonly string ThreadUpdated = nameof(ThreadUpdated);
    public static readonly string ThreadMemberAdded = nameof(ThreadMemberAdded);
    public static readonly string ThreadMemberRemoved = nameof(ThreadMemberRemoved);
    public static readonly string MessageCreated = nameof(MessageCreated);
    public static readonly string MessageEdited = nameof(MessageEdited);
    public static readonly string MessageDeleted = nameof(MessageDeleted);
    public static readonly string ReactionCreated = nameof(ReactionCreated);
    public static readonly string ReactionDeleted = nameof(ReactionDeleted);
    public static readonly string MessagesRead = nameof(MessagesRead);
    public static readonly string ThreadMuted = nameof(ThreadMuted);
    public static readonly string ThreadArchived = nameof(ThreadArchived);
    public static readonly string ThreadLeft = nameof(ThreadLeft);
    public static readonly string ThreadInviteCreated = nameof(ThreadInviteCreated);
    public static readonly string ThreadInviteAccepted = nameof(ThreadInviteAccepted);
    public static readonly string ThreadInviteDeclined = nameof(ThreadInviteDeclined);
    public static readonly string ThreadMemberRoleChanged = nameof(ThreadMemberRoleChanged);
    public static readonly string MessagePinned = nameof(MessagePinned);
    public static readonly string MessageUnpinned = nameof(MessageUnpinned);
    public static readonly string MessageSaved = nameof(MessageSaved);
    public static readonly string MessageUnsaved = nameof(MessageUnsaved);
    public static readonly string MessageFileAttached = nameof(MessageFileAttached);
    public static readonly string MessageFileDetached = nameof(MessageFileDetached);
    public static readonly string MessageReported = nameof(MessageReported);
    public static readonly string CredentialBlocked = nameof(CredentialBlocked);
    public static readonly string CredentialUnblocked = nameof(CredentialUnblocked);

    // TODO: Add audio/video call event types only after call/session feature flags exist in Communications.
}

public static class MessageRealtimeTopics
{
    public static readonly string EventName = "CommunicationsRealtimeEvent";
    public static readonly string TypingEventName = "CommunicationsTypingState";
    public static readonly string PresenceEventName = "CommunicationsPresenceState";

    public static string User(Guid tenantId, Guid credentialId) =>
        $"communications.tenant.{tenantId:N}.user.{credentialId:N}";

    public static string ThreadTyping(Guid tenantId, Guid threadId) =>
        $"communications.tenant.{tenantId:N}.thread.{threadId:N}.typing";

    public static string Presence(Guid tenantId) =>
        $"communications.tenant.{tenantId:N}.presence";
}


public static class GenericSender
{
    public static readonly string System = "+630000000000";
}

public static class MessageDeliveryTypes
{
    public static readonly Guid Delivered = new("b1000000-0000-0000-0000-000000000001");
    public static readonly Guid Read = new("b1000000-0000-0000-0000-000000000002");
}

public static class MessageThreadMemberRoles
{
    public const string Owner = nameof(Owner);
    public const string Admin = nameof(Admin);
    public const string Member = nameof(Member);
}

public static class MessageThreadInviteStatuses
{
    public const short Pending = 0;
    public const short Accepted = 1;
    public const short Declined = 2;
}

public static class MessageReportStatuses
{
    public const short Open = 0;
    public const short Reviewed = 1;
    public const short Dismissed = 2;
    public const short Resolved = 3;
    public const short Escalated = 4;
}

public static class MessageModerationRuleMatchTypes
{
    public const string Keyword = "keyword";
    public const string Regex = "regex";
}

public static class MessageModerationRuleActions
{
    public const string Flag = "flag";
    public const string AutoReport = "auto-report";
    public const string BlockBeforeSend = "block-before-send";
}

public static class MessageReportAuditActions
{
    public const string Reviewed = "reviewed";
    public const string Assigned = "assigned";
    public const string Dismissed = "dismissed";
    public const string Resolved = "resolved";
    public const string Escalated = "escalated";
    public const string NoteAdded = "note-added";
    public const string AutoReported = "auto-reported";
}
