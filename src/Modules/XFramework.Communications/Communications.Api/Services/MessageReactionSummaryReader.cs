using Communications.Domain.Shared.Contracts.Responses;

namespace Communications.Api.Services;

public interface IMessageReactionSummaryReader
{
    Task<Dictionary<Guid, List<MessageReactionSummaryResponse>>> ReadAsync(
        Guid tenantId, Guid credentialId, IReadOnlyCollection<Guid> visibleMessageIds, CancellationToken ct);
}

// Only called after ThreadService has selected the visible, membership-authorized page.
// Aggregate in PostgreSQL, not by loading every participant's reaction into the Portal/app.
public sealed class MessageReactionSummaryReader(DbContext db) : IMessageReactionSummaryReader
{
    public async Task<Dictionary<Guid, List<MessageReactionSummaryResponse>>> ReadAsync(
        Guid tenantId, Guid credentialId, IReadOnlyCollection<Guid> visibleMessageIds, CancellationToken ct)
    {
        if (visibleMessageIds.Count == 0)
            return [];
        if (visibleMessageIds.Count > 100)
            throw new ArgumentOutOfRangeException(nameof(visibleMessageIds));

        var query = from reaction in db.Set<MessageReaction>().AsNoTracking()
                    join type in db.Set<MessageReactionType>().AsNoTracking() on reaction.TypeId equals type.Id
                    join member in db.Set<MessageThreadMember>().AsNoTracking() on reaction.MessageThreadMemberId equals member.Id
                    where reaction.TenantId == tenantId && type.TenantId == tenantId && member.TenantId == tenantId
                        && !reaction.IsDeleted && reaction.IsEnabled && !type.IsDeleted && type.IsEnabled
                        && visibleMessageIds.Contains(reaction.MessageId)
                    select new { reaction.Id, reaction.MessageId, reaction.TypeId, type.Name, type.Emoji, member.CredentialId };

        var counts = await query
            .GroupBy(x => new { x.MessageId, x.TypeId, x.Name, x.Emoji })
            .Select(x => new { x.Key.MessageId, x.Key.TypeId, x.Key.Name, x.Key.Emoji, Count = x.Count() })
            .ToListAsync(ct);
        var mine = await query.Where(x => x.CredentialId == credentialId)
            .Select(x => new { x.MessageId, x.TypeId, x.Id }).ToListAsync(ct);
        var ownIds = mine.ToDictionary(x => (x.MessageId, x.TypeId), x => x.Id);
        return counts.GroupBy(x => x.MessageId).ToDictionary(
            group => group.Key,
            group => group.Select(x => new MessageReactionSummaryResponse
            {
                TypeId = x.TypeId, Name = x.Name, Emoji = x.Emoji, Count = x.Count,
                MyReactionId = ownIds.TryGetValue((x.MessageId, x.TypeId), out var id) ? id : null
            }).ToList());
    }
}
