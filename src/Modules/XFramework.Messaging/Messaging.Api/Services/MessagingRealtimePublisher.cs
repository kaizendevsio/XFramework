using Messaging.Domain.Shared;
using Messaging.Domain.Shared.Contracts.Realtime;
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
            return await dbContext.Set<MessageThreadMember>()
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(m => m.TenantId == outboxEvent.TenantId)
                .Where(m => m.MessageThreadId == threadId)
                .Where(m => !m.IsDeleted && m.IsEnabled)
                .Select(m => m.CredentialId)
                .Distinct()
                .ToListAsync(ct);
        }

        return outboxEvent.ActorCredentialId is Guid actorCredentialId
            ? [actorCredentialId]
            : [];
    }
}
