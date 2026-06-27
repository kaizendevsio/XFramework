using System.Text.Json;
using IdentityServer.Domain.Shared.Contracts;
using Messaging.Domain.Shared;
using Notifications.Domain.Shared.Contracts;
using Notifications.Domain.Shared.Contracts.Requests;
using Notifications.Domain.Shared.Enums;
using Notifications.Integration.Drivers;
using XFramework.Core.Services.FeatureGates;

namespace Messaging.Api.Services;

public sealed class MessagingNotificationFanout(
    AppDbContext db,
    ITenantModuleFeatureService featureService,
    INotificationsServiceWrapper notificationsWrapper,
    ILogger<MessagingNotificationFanout> logger) : IMessagingNotificationFanout
{
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
            logger.LogError(
                ex,
                "Messaging notification fanout failed for outbox event {OutboxEventId}",
                outboxEvent.Id);
            throw;
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

        var senderMember = await db.Set<MessageThreadMember>()
            .AsNoTracking()
            .Where(m => m.Id == message.MessageThreadMemberId)
            .Where(m => m.TenantId == outboxEvent.TenantId)
            .Where(m => !m.IsDeleted && m.IsEnabled)
            .FirstOrDefaultAsync(ct);
        if (senderMember is null)
            return;

        var members = await ActiveMembersAsync(outboxEvent.TenantId, threadId, ct);
        var mentionIds = DeserializeMentionedCredentialIds(message.MentionedCredentialIdsJson).ToHashSet();

        foreach (var member in members)
        {
            if (member.CredentialId == outboxEvent.ActorCredentialId || member.IsMuted || member.IsArchived)
                continue;

            if (await IsBlockedAsync(outboxEvent.TenantId, member.CredentialId, senderMember.CredentialId, ct))
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

        var reactor = await db.Set<MessageThreadMember>()
            .AsNoTracking()
            .Where(m => m.Id == reaction.MessageThreadMemberId)
            .Where(m => m.TenantId == outboxEvent.TenantId)
            .FirstOrDefaultAsync(ct);
        if (reactor is null)
            return;

        if (await IsBlockedAsync(outboxEvent.TenantId, author.CredentialId, reactor.CredentialId, ct))
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
        var response = await notificationsWrapper.CreateNotification(new CreateNotificationRequest
        {
            TenantId = outboxEvent.TenantId,
            Metadata = new RequestMetadata { TenantId = outboxEvent.TenantId },
            RecipientCredentialId = recipientCredentialId,
            SourceCredentialId = outboxEvent.ActorCredentialId,
            TemplateKey = templateKey,
            Title = title,
            Body = body,
            DeliveryChannels = NotificationPreferenceDefaults.EnabledChannels,
            CorrelationId = correlationId,
            Data = ToNotificationData(data)
        }, ct);

        if (response.IsSuccess)
            return;

        if (response.HttpStatusCode == System.Net.HttpStatusCode.Conflict)
        {
            logger.LogDebug(
                "Notification suppressed by Notifications module. OutboxEventId={OutboxEventId} RecipientCredentialId={CredentialId} TemplateKey={TemplateKey}: {Message}",
                outboxEvent.Id,
                recipientCredentialId,
                templateKey,
                response.Message);
            return;
        }

        throw new InvalidOperationException(
            $"Notifications module rejected Messaging fanout for {recipientCredentialId}: {response.HttpStatusCode} {response.Message}");
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

    private async Task<bool> IsBlockedAsync(
        Guid tenantId,
        Guid firstCredentialId,
        Guid secondCredentialId,
        CancellationToken ct) =>
        await db.Set<MessageBlock>()
            .AsNoTracking()
            .Where(b => b.TenantId == tenantId)
            .Where(b => !b.IsDeleted && b.IsEnabled)
            .Where(b =>
                (b.BlockerCredentialId == firstCredentialId && b.BlockedCredentialId == secondCredentialId) ||
                (b.BlockerCredentialId == secondCredentialId && b.BlockedCredentialId == firstCredentialId))
            .AnyAsync(ct);

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

    private static Dictionary<string, string> ToNotificationData(object data)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(data));
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var property in document.RootElement.EnumerateObject())
        {
            result[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => property.Value.GetRawText(),
                JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                _ => property.Value.GetRawText()
            };
        }

        return result;
    }
}
