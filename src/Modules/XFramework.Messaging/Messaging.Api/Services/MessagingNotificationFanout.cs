using System.Text.Json;
using IdentityServer.Domain.Shared.Contracts;
using Messaging.Domain.Shared;
using Notifications.Domain.Shared.Contracts;
using Notifications.Domain.Shared.Enums;
using XFramework.Core.Services.FeatureGates;

namespace Messaging.Api.Services;

public sealed class MessagingNotificationFanout(
    AppDbContext db,
    ITenantModuleFeatureService featureService,
    ILogger<MessagingNotificationFanout> logger) : IMessagingNotificationFanout
{
    private const char TemplateKeySeparator = '\n';

    public async Task CreateNotificationsAsync(MessageOutboxEvent outboxEvent, CancellationToken ct = default)
    {
        if (!await NotificationsEnabledAsync(outboxEvent.TenantId, ct))
            return;

        try
        {
            switch (outboxEvent.EventType)
            {
                case var eventType when eventType == MessageRealtimeEvents.MessageCreated:
                    await CreateMessageNotificationsAsync(outboxEvent, ct);
                    break;
                case var eventType when eventType == MessageRealtimeEvents.ThreadInviteCreated:
                    await CreateInviteNotificationAsync(outboxEvent, ct);
                    break;
                case var eventType when eventType == MessageRealtimeEvents.ReactionCreated:
                    await CreateReactionNotificationAsync(outboxEvent, ct);
                    break;
                case var eventType when eventType == MessageRealtimeEvents.MessageReported:
                    await CreateModerationNotificationsAsync(outboxEvent, ct);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Messaging notification fanout skipped for outbox event {OutboxEventId}",
                outboxEvent.Id);
        }
    }

    private async Task CreateMessageNotificationsAsync(MessageOutboxEvent outboxEvent, CancellationToken ct)
    {
        if (outboxEvent.ThreadId is not Guid threadId)
            return;

        var message = await db.Set<Message>()
            .AsNoTracking()
            .Where(m => m.Id == outboxEvent.AggregateId)
            .Where(m => m.TenantId == outboxEvent.TenantId)
            .FirstOrDefaultAsync(ct);
        if (message is null)
            return;

        var members = await ActiveMembersAsync(outboxEvent.TenantId, threadId, ct);
        var mentionIds = DeserializeMentionedCredentialIds(message.MentionedCredentialIdsJson).ToHashSet();

        foreach (var member in members)
        {
            if (member.CredentialId == outboxEvent.ActorCredentialId || member.IsMuted || member.IsArchived)
                continue;

            var isMention = mentionIds.Contains(member.CredentialId);
            await AddNotificationAsync(
                outboxEvent,
                member.CredentialId,
                isMention ? NotificationTemplateKeys.CommunityMention : NotificationTemplateKeys.MessageReceived,
                isMention ? "You were mentioned" : "New message",
                TrimPreview(message.Text),
                new
                {
                    outboxEvent.ThreadId,
                    MessageId = message.Id,
                    Mentioned = isMention
                },
                ct);
        }
    }

    private async Task CreateInviteNotificationAsync(MessageOutboxEvent outboxEvent, CancellationToken ct)
    {
        var invite = await db.Set<MessageThreadInvite>()
            .AsNoTracking()
            .Where(i => i.Id == outboxEvent.AggregateId)
            .Where(i => i.TenantId == outboxEvent.TenantId)
            .FirstOrDefaultAsync(ct);
        if (invite is null || invite.InvitedCredentialId == outboxEvent.ActorCredentialId)
            return;

        await AddNotificationAsync(
            outboxEvent,
            invite.InvitedCredentialId,
            NotificationTemplateKeys.SystemGeneric,
            "Thread invite",
            "You were invited to a message thread.",
            new
            {
                invite.MessageThreadId,
                InviteId = invite.Id
            },
            ct);
    }

    private async Task CreateReactionNotificationAsync(MessageOutboxEvent outboxEvent, CancellationToken ct)
    {
        var reaction = await db.Set<MessageReaction>()
            .AsNoTracking()
            .Where(r => r.Id == outboxEvent.AggregateId)
            .Where(r => r.TenantId == outboxEvent.TenantId)
            .FirstOrDefaultAsync(ct);
        if (reaction is null)
            return;

        var message = await db.Set<Message>()
            .AsNoTracking()
            .Where(m => m.Id == reaction.MessageId)
            .Where(m => m.TenantId == outboxEvent.TenantId)
            .FirstOrDefaultAsync(ct);
        if (message is null)
            return;

        var author = await db.Set<MessageThreadMember>()
            .AsNoTracking()
            .Where(m => m.Id == message.MessageThreadMemberId)
            .Where(m => m.TenantId == outboxEvent.TenantId)
            .FirstOrDefaultAsync(ct);
        if (author is null || author.CredentialId == outboxEvent.ActorCredentialId || author.IsMuted || author.IsArchived)
            return;

        await AddNotificationAsync(
            outboxEvent,
            author.CredentialId,
            NotificationTemplateKeys.CommunityReaction,
            "New reaction",
            "Someone reacted to your message.",
            new
            {
                MessageId = message.Id,
                message.MessageThreadId,
                ReactionId = reaction.Id
            },
            ct);
    }

    private async Task CreateModerationNotificationsAsync(MessageOutboxEvent outboxEvent, CancellationToken ct)
    {
        if (outboxEvent.ThreadId is not Guid threadId)
            return;

        var admins = await db.Set<MessageThreadMember>()
            .AsNoTracking()
            .Where(m => m.TenantId == outboxEvent.TenantId)
            .Where(m => m.MessageThreadId == threadId)
            .Where(m => m.Role == MessageThreadMemberRoles.Owner || m.Role == MessageThreadMemberRoles.Admin)
            .Where(m => !m.IsDeleted && m.IsEnabled)
            .ToListAsync(ct);

        foreach (var admin in admins.Where(admin => admin.CredentialId != outboxEvent.ActorCredentialId))
        {
            await AddNotificationAsync(
                outboxEvent,
                admin.CredentialId,
                NotificationTemplateKeys.SystemGeneric,
                "Message reported",
                "A message was reported for moderation review.",
                new
                {
                    outboxEvent.ThreadId,
                    ReportId = outboxEvent.AggregateId
                },
                ct);
        }
    }

    private async Task AddNotificationAsync(
        MessageOutboxEvent outboxEvent,
        Guid recipientCredentialId,
        string templateKey,
        string title,
        string body,
        object data,
        CancellationToken ct)
    {
        var correlationId = $"messaging:{outboxEvent.Id:N}:{recipientCredentialId:N}:{templateKey}";
        var exists = await db.Set<NotificationInboxItem>()
            .AnyAsync(item =>
                item.TenantId == outboxEvent.TenantId &&
                item.CorrelationId == correlationId,
                ct);
        if (exists)
            return;

        var preferences = await db.Set<NotificationPreference>()
            .AsNoTracking()
            .Where(p => p.TenantId == outboxEvent.TenantId)
            .Where(p => p.CredentialId == recipientCredentialId)
            .Where(p => !p.IsDeleted && p.IsEnabled)
            .FirstOrDefaultAsync(ct);

        if (IsTemplateDisabled(preferences, templateKey))
            return;

        var enabledChannels = preferences?.EnabledChannels ?? NotificationPreferenceDefaults.EnabledChannels;
        var effectiveChannels = NotificationPreferenceDefaults.EnabledChannels & enabledChannels;
        if (effectiveChannels == NotificationDeliveryChannel.None)
            return;

        db.Set<NotificationInboxItem>().Add(new NotificationInboxItem
        {
            Id = Guid.NewGuid(),
            TenantId = outboxEvent.TenantId,
            RecipientCredentialId = recipientCredentialId,
            SourceCredentialId = outboxEvent.ActorCredentialId,
            TemplateKey = templateKey,
            Title = title,
            Body = body,
            DeliveryChannels = effectiveChannels,
            CorrelationId = correlationId,
            DataJson = JsonSerializer.Serialize(data),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            ConcurrencyStamp = Guid.NewGuid()
        });
    }

    private async Task<bool> NotificationsEnabledAsync(Guid tenantId, CancellationToken ct)
    {
        try
        {
            var result = await featureService.EnsureEnabledAsync(tenantId, TenantModuleFeatureKeys.Notifications, string.Empty, ct);
            return result.IsSuccess;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Notifications feature check failed for tenant {TenantId}", tenantId);
            return false;
        }
    }

    private async Task<List<MessageThreadMember>> ActiveMembersAsync(Guid tenantId, Guid threadId, CancellationToken ct) =>
        await db.Set<MessageThreadMember>()
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId)
            .Where(m => m.MessageThreadId == threadId)
            .Where(m => !m.IsDeleted && m.IsEnabled)
            .ToListAsync(ct);

    private static bool IsTemplateDisabled(NotificationPreference? preference, string templateKey) =>
        preference?.DisabledTemplateKeys?
            .Split(TemplateKeySeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Contains(templateKey, StringComparer.OrdinalIgnoreCase) == true;

    private static List<Guid> DeserializeMentionedCredentialIds(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string TrimPreview(string text) =>
        text.Length <= 160 ? text : text[..160] + "...";
}
