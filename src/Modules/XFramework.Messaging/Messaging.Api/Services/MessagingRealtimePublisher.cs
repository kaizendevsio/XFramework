using Messaging.Domain.Shared;
using Messaging.Domain.Shared.Contracts;
using Messaging.Domain.Shared.Contracts.Realtime;
using System.Text.Json;
using XFramework.Integration.Abstractions.Wrappers;

namespace Messaging.Api.Services;

public sealed class MessagingRealtimePublisher(
    DbContext dbContext,
    IMessageBusWrapper messageBus,
    ILogger<MessagingRealtimePublisher> logger) : IMessagingRealtimePublisher
{
    public async Task PublishAsync(MessageOutboxEvent outboxEvent, CancellationToken ct = default)
    {
        var recipients = await ResolveRecipientsAsync(outboxEvent, ct);
        if (recipients.Count == 0)
        {
            logger.LogDebug("Messaging outbox event {EventId} has no realtime recipients", outboxEvent.Id);
            return;
        }

        var envelope = new MessagingRealtimeEvent
        {
            EventId = outboxEvent.Id,
            TenantId = outboxEvent.TenantId,
            ThreadId = outboxEvent.ThreadId,
            ActorCredentialId = outboxEvent.ActorCredentialId,
            EventType = outboxEvent.EventType,
            OccurredAt = outboxEvent.OccurredAt,
            Sequence = outboxEvent.OccurredAt.Ticks,
            PayloadJson = outboxEvent.PayloadJson
        };

        foreach (var credentialId in recipients)
        {
            var topic = MessageRealtimeTopics.User(outboxEvent.TenantId, credentialId);
            await messageBus.PublishAsync(MessageRealtimeTopics.EventName, topic, envelope, durable: true);
        }
    }

    private async Task<List<Guid>> ResolveRecipientsAsync(MessageOutboxEvent outboxEvent, CancellationToken ct)
    {
        if (outboxEvent.ThreadId is Guid threadId)
        {
            if (IsModerationEvent(outboxEvent))
                return await ResolveModerationRecipientsAsync(outboxEvent, threadId, ct);

            var contentMessageId = await ResolveContentMessageIdAsync(outboxEvent, ct);
            var contentSenderCredentialId = await ResolveContentSenderCredentialIdAsync(outboxEvent, ct);
            var members = await dbContext.Set<MessageThreadMember>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(m => m.TenantId == outboxEvent.TenantId)
                .Where(m => m.MessageThreadId == threadId)
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .ToListAsync(ct);
            if (members.Count == 0)
                return [];

            var recipientMembers = members;
            if (contentMessageId is Guid messageId && messageId != Guid.Empty)
            {
                var memberIds = members.Select(member => member.Id).ToList();
                var hiddenMemberIds = await dbContext.Set<MessageHidden>()
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(h => h.TenantId == outboxEvent.TenantId)
                    .Where(h => h.MessageId == messageId)
                    .Where(h => memberIds.Contains(h.MessageThreadMemberId))
                    .Where(h => !h.IsDeleted && h.IsEnabled)
                    .Select(h => h.MessageThreadMemberId)
                    .Distinct()
                    .ToListAsync(ct);

                if (hiddenMemberIds.Count > 0)
                {
                    var hiddenSet = hiddenMemberIds.ToHashSet();
                    recipientMembers = members
                        .Where(member => !hiddenSet.Contains(member.Id))
                        .ToList();
                }
            }

            var recipients = recipientMembers
                .Select(m => m.CredentialId)
                .Distinct()
                .ToList();

            if (contentSenderCredentialId is not Guid senderCredentialId || recipients.Count == 0)
                return recipients;

            var blockedRecipientIds = await dbContext.Set<MessageBlock>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(b => b.TenantId == outboxEvent.TenantId)
                .Where(b => !b.IsDeleted && b.IsEnabled)
                .Where(b =>
                    (b.BlockerCredentialId == senderCredentialId && recipients.Contains(b.BlockedCredentialId)) ||
                    (b.BlockedCredentialId == senderCredentialId && recipients.Contains(b.BlockerCredentialId)))
                .Select(b => b.BlockerCredentialId == senderCredentialId ? b.BlockedCredentialId : b.BlockerCredentialId)
                .Distinct()
                .ToListAsync(ct);

            if (blockedRecipientIds.Count == 0)
                return recipients;

            var blockedSet = blockedRecipientIds.ToHashSet();
            return recipients.Where(credentialId => !blockedSet.Contains(credentialId)).ToList();
        }

        return outboxEvent.ActorCredentialId is Guid actorId
            ? [actorId]
            : [];
    }

    private async Task<List<Guid>> ResolveModerationRecipientsAsync(
        MessageOutboxEvent outboxEvent,
        Guid threadId,
        CancellationToken ct) =>
        await dbContext.Set<MessageThreadMember>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.TenantId == outboxEvent.TenantId)
            .Where(m => m.MessageThreadId == threadId)
            .Where(m => !m.IsDeleted && m.IsEnabled)
            .Where(m => m.Role == MessageThreadMemberRoles.Owner || m.Role == MessageThreadMemberRoles.Admin)
            .Where(m => m.CredentialId != outboxEvent.ActorCredentialId)
            .Select(m => m.CredentialId)
            .Distinct()
            .ToListAsync(ct);

    private static bool IsModerationEvent(MessageOutboxEvent outboxEvent) =>
        outboxEvent.EventType == MessageRealtimeEvents.MessageReported ||
        outboxEvent.AggregateType == nameof(MessageReport);

    private async Task<Guid?> ResolveContentSenderCredentialIdAsync(MessageOutboxEvent outboxEvent, CancellationToken ct)
    {
        var messageId = await ResolveContentMessageIdAsync(outboxEvent, ct);
        if (messageId is not Guid resolvedMessageId || resolvedMessageId == Guid.Empty)
            return outboxEvent.ActorCredentialId;

        return await (
                from message in dbContext.Set<Message>().IgnoreQueryFilters().AsNoTracking()
                join member in dbContext.Set<MessageThreadMember>().IgnoreQueryFilters().AsNoTracking()
                    on message.MessageThreadMemberId equals member.Id
                where message.TenantId == outboxEvent.TenantId &&
                      message.Id == resolvedMessageId
                select (Guid?)member.CredentialId)
            .FirstOrDefaultAsync(ct) ?? outboxEvent.ActorCredentialId;
    }

    private async Task<Guid?> ResolveContentMessageIdAsync(MessageOutboxEvent outboxEvent, CancellationToken ct) =>
        outboxEvent.AggregateType switch
        {
            nameof(Message) => outboxEvent.AggregateId,
            nameof(MessageReaction) => await ResolveMessageIdForReactionAsync(outboxEvent, ct),
            nameof(MessageFile) => await ResolveMessageIdForFileAsync(outboxEvent, ct),
            nameof(MessagePin) => TryReadGuid(outboxEvent.PayloadJson, "MessageId") ?? outboxEvent.AggregateId,
            nameof(MessageReport) => TryReadGuid(outboxEvent.PayloadJson, "MessageId") ?? outboxEvent.AggregateId,
            _ => TryReadGuid(outboxEvent.PayloadJson, "MessageId")
        };

    private async Task<Guid?> ResolveMessageIdForReactionAsync(MessageOutboxEvent outboxEvent, CancellationToken ct)
    {
        var reactionId = TryReadGuid(outboxEvent.PayloadJson, "ReactionId") ?? outboxEvent.AggregateId;
        return await dbContext.Set<MessageReaction>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(r => r.TenantId == outboxEvent.TenantId)
            .Where(r => r.Id == reactionId)
            .Select(r => (Guid?)r.MessageId)
            .FirstOrDefaultAsync(ct)
            ?? TryReadGuid(outboxEvent.PayloadJson, "MessageId");
    }

    private async Task<Guid?> ResolveMessageIdForFileAsync(MessageOutboxEvent outboxEvent, CancellationToken ct)
    {
        var fileId = TryReadGuid(outboxEvent.PayloadJson, "FileId") ?? outboxEvent.AggregateId;
        return await dbContext.Set<MessageFile>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(f => f.TenantId == outboxEvent.TenantId)
            .Where(f => f.Id == fileId)
            .Select(f => (Guid?)f.MessageId)
            .FirstOrDefaultAsync(ct)
            ?? TryReadGuid(outboxEvent.PayloadJson, "MessageId");
    }

    private static Guid? TryReadGuid(string payloadJson, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            return null;

        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String &&
                    Guid.TryParse(property.Value.GetString(), out var id))
                    return id;
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }
}
