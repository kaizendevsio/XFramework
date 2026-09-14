namespace Communications.Api.Services;

public sealed record MessageReceiptPositions(Guid? LatestOwnId, Dictionary<Guid, Guid> LastReadByMember);
public interface IMessageReceiptPositionReader
{
    Task<MessageReceiptPositions> ReadAsync(Guid tenant, Guid thread, Guid sender, Guid? parent,
        IReadOnlyCollection<Guid> hidden, IReadOnlyCollection<Guid> readers, Guid? readType, CancellationToken ct);
}

// Membership and receipt feature policy are checked by ThreadService first.
public sealed class MessageReceiptPositionReader(DbContext db) : IMessageReceiptPositionReader
{
    public async Task<MessageReceiptPositions> ReadAsync(Guid tenant, Guid thread, Guid sender, Guid? parent,
        IReadOnlyCollection<Guid> hidden, IReadOnlyCollection<Guid> readers, Guid? readType, CancellationToken ct)
    {
        var messages = db.Set<Message>().AsNoTracking().Where(m => m.TenantId == tenant && m.MessageThreadId == thread
            && m.MessageThreadMemberId == sender && m.IsEnabled && !m.IsDeleted && !hidden.Contains(m.Id));
        messages = parent.HasValue ? messages.Where(m => m.ParentMessageId == parent) : messages.Where(m => !m.IsThreadReply);
        var latest = await messages.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).Select(m => (Guid?)m.Id).FirstOrDefaultAsync(ct);
        if (readers.Count == 0 || !readType.HasValue) return new(latest, []);
        // Projection is one ID per reader across all history, never all messages.
        var positions = await (from delivery in db.Set<MessageDelivery>().AsNoTracking()
            join message in messages on delivery.MessageId equals message.Id
            where delivery.TenantId == tenant && delivery.TypeId == readType && delivery.IsEnabled && !delivery.IsDeleted
                && readers.Contains(delivery.MessageThreadMemberId)
            select new { MemberId = delivery.MessageThreadMemberId, MessageId = message.Id, message.CreatedAt })
            .GroupBy(x => x.MemberId)
            .Select(g => g.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.MessageId).First())
            .ToDictionaryAsync(x => x.MemberId, x => x.MessageId, ct);
        return new(latest, positions);
    }
}
