using Communications.Domain.Shared.Contracts.Requests.Reactions;
using Communications.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts.Responses;

namespace Communications.Api.Services;

public sealed partial class ThreadService
{
    public async Task<Result<PaginatedResult<MessageReactionResponse>>> GetMessageReactionsAsync(
        GetMessageReactionsRequest request, CancellationToken ct = default)
    {
        if (request.ThreadId == Guid.Empty || request.MessageId == Guid.Empty ||
            request.PageIndex is < 0 or > 100 || request.PageSize is < 1 or > 100)
            return Result<PaginatedResult<MessageReactionResponse>>.Failure("Invalid reaction query", 400);

        var callerResult = await ResolveCallerAsync(request.Metadata, ct);
        if (!callerResult.IsSuccess)
            return CallerFailure<PaginatedResult<MessageReactionResponse>>(callerResult);
        var caller = callerResult.Data!;

        var member = await dataContext.Query<MessageThreadMember>()
            .Where(x => x.TenantId == caller.TenantId && x.MessageThreadId == request.ThreadId)
            .Where(x => x.CredentialId == caller.CredentialId && !x.IsDeleted && x.IsEnabled)
            .FirstOrDefaultAsync(ct);
        if (member is null)
            return Result<PaginatedResult<MessageReactionResponse>>.Forbidden("Requester is not a member of this thread");

        var message = await dataContext.Query<Message>()
            .Where(x => x.Id == request.MessageId && x.MessageThreadId == request.ThreadId)
            .Where(x => x.TenantId == caller.TenantId && !x.IsDeleted && x.IsEnabled)
            .FirstOrDefaultAsync(ct);
        if (message is null || !await CanAccessMessageAsync(caller.TenantId, member, message, ct))
            return Result<PaginatedResult<MessageReactionResponse>>.NotFound("Message not found");

        var query = dataContext.Query<MessageReaction>()
            .Where(x => x.TenantId == caller.TenantId && x.MessageId == request.MessageId)
            .Where(x => !x.IsDeleted && x.IsEnabled);
        var count = await query.CountAsync(ct);
        var reactions = await query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip(request.PageIndex * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        var typeIds = reactions.Select(x => x.TypeId).Distinct().ToList();
        var memberIds = reactions.Select(x => x.MessageThreadMemberId).Distinct().ToList();
        var types = await dataContext.Query<MessageReactionType>()
            .Where(x => x.TenantId == caller.TenantId && typeIds.Contains(x.Id))
            .Where(x => !x.IsDeleted && x.IsEnabled).ToListAsync(ct);
        var members = await dataContext.Query<MessageThreadMember>()
            .Where(x => x.TenantId == caller.TenantId && x.MessageThreadId == request.ThreadId)
            .Where(x => memberIds.Contains(x.Id)).ToListAsync(ct);
        var typeMap = types.ToDictionary(x => x.Id);
        var memberMap = members.ToDictionary(x => x.Id);

        return Result<PaginatedResult<MessageReactionResponse>>.Success(new(
            count, request.PageIndex, request.PageSize,
            reactions.Where(x => typeMap.ContainsKey(x.TypeId) && memberMap.ContainsKey(x.MessageThreadMemberId))
                .Select(x => new MessageReactionResponse
                {
                    Id = x.Id, MessageId = x.MessageId, TypeId = x.TypeId,
                    MemberId = x.MessageThreadMemberId, CredentialId = memberMap[x.MessageThreadMemberId].CredentialId,
                    Name = typeMap[x.TypeId].Name, Emoji = typeMap[x.TypeId].Emoji, CreatedAt = x.CreatedAt
                }).ToList()));
    }

    private async Task<Guid?> ResolveDeliveryTypeIdAsync(Guid tenantId, Guid referenceId, CancellationToken ct)
    {
        var type = await dataContext.Query<MessageDeliveryType>()
            .Where(x => x.TenantId == tenantId && !x.IsDeleted && x.IsEnabled)
            .Where(x => x.SystemReferenceId == referenceId || x.Id == referenceId)
            .FirstOrDefaultAsync(ct);
        return type?.Id;
    }
}
