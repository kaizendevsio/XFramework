namespace Communications.Api.Services;

public interface IMessageReplySummaryReader
{
    Task<Dictionary<Guid, int>> ReadAsync(Guid tenantId, Guid threadId,
        IReadOnlyCollection<Guid> visibleMessageIds, IReadOnlyCollection<Guid> blockedMemberIds,
        IReadOnlyCollection<Guid> hiddenMessageIds, CancellationToken ct);
}

// ThreadService authorizes membership and the visible page before calling this reader.
public sealed class MessageReplySummaryReader(DbContext db) : IMessageReplySummaryReader
{
    public async Task<Dictionary<Guid, int>> ReadAsync(Guid tenantId, Guid threadId,
        IReadOnlyCollection<Guid> visibleMessageIds, IReadOnlyCollection<Guid> blockedMemberIds,
        IReadOnlyCollection<Guid> hiddenMessageIds, CancellationToken ct)
    {
        if (visibleMessageIds.Count == 0) return [];
        if (visibleMessageIds.Count > 100) throw new ArgumentOutOfRangeException(nameof(visibleMessageIds));

        var query = db.Set<Message>().AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.MessageThreadId == threadId && !x.IsDeleted && x.IsEnabled
                && x.ParentMessageId.HasValue && visibleMessageIds.Contains(x.ParentMessageId.Value));
        if (blockedMemberIds.Count > 0) query = query.Where(x => !blockedMemberIds.Contains(x.MessageThreadMemberId));
        if (hiddenMessageIds.Count > 0) query = query.Where(x => !hiddenMessageIds.Contains(x.Id));

        return await query.GroupBy(x => x.ParentMessageId!.Value)
            .Select(group => new { Id = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);
    }
}
