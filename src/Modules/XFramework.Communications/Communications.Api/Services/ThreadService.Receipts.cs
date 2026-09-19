using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts.Requests.Receipts;
using Communications.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;

namespace Communications.Api.Services;

public sealed partial class ThreadService
{
    /// <summary>When each member received and read one message. The message page carries counts only,
    /// on purpose - a hundred-member group would otherwise pay for a hundred timestamps on every
    /// message of every page. This is the on-demand read behind a single message's status detail.</summary>
    public async Task<Result<GetMessageReceiptsResponse>> GetMessageReceiptsAsync(
        GetMessageReceiptsRequest request, CancellationToken ct = default)
    {
        if (request.ThreadId == Guid.Empty || request.MessageId == Guid.Empty ||
            request.PageIndex is < 0 or > 100 || request.PageSize is < 1 or > 100)
            return Result<GetMessageReceiptsResponse>.Failure("Invalid receipt query", 400);

        var callerResult = await ResolveCallerAsync(request.Metadata, ct);
        if (!callerResult.IsSuccess)
            return CallerFailure<GetMessageReceiptsResponse>(callerResult);
        var caller = callerResult.Data!;

        var member = await dataContext.Query<MessageThreadMember>()
            .Where(x => x.TenantId == caller.TenantId && x.MessageThreadId == request.ThreadId)
            .Where(x => x.CredentialId == caller.CredentialId && !x.IsDeleted && x.IsEnabled)
            .FirstOrDefaultAsync(ct);
        if (member is null)
            return Result<GetMessageReceiptsResponse>.Forbidden("Requester is not a member of this thread");

        var message = await dataContext.Query<Message>()
            .Where(x => x.Id == request.MessageId && x.MessageThreadId == request.ThreadId)
            .Where(x => x.TenantId == caller.TenantId && !x.IsDeleted && x.IsEnabled)
            .FirstOrDefaultAsync(ct);
        if (message is null || !await CanAccessMessageAsync(caller.TenantId, member, message, ct))
            return Result<GetMessageReceiptsResponse>.NotFound("Message not found");
        // The same rule the message projection enforces: a receipt belongs to the sender of the
        // message, never to whoever can see it. Reading another member's state is not a smaller
        // version of this feature, it is a different one - so refuse rather than return nothing.
        if (message.MessageThreadMemberId != member.Id)
            return Result<GetMessageReceiptsResponse>.Forbidden("Receipts are only visible to the sender of a message");

        // Never count the sender's own fetch of their own message.
        var query = dataContext.Query<MessageDelivery>()
            .Where(x => x.TenantId == caller.TenantId && x.MessageId == request.MessageId)
            .Where(x => x.MessageThreadMemberId != member.Id && !x.IsDeleted && x.IsEnabled);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip(request.PageIndex * request.PageSize).Take(request.PageSize).ToListAsync(ct);

        var readTypeId = await ResolveDeliveryTypeIdAsync(caller.TenantId, MessageDeliveryTypes.Read, ct);
        var allowReadReceipts = readTypeId.HasValue
            && await FeatureEnabledAsync(caller.TenantId, request.ThreadId, ConversationFeatures.ReadReceipts, ct)
            && (await policyService.GetPolicyAsync(caller.TenantId, ct)).ReadReceiptsEnabled;
        var memberIds = rows.Select(x => x.MessageThreadMemberId).Distinct().ToList();
        var credentials = (await dataContext.Query<MessageThreadMember>()
            .Where(x => x.TenantId == caller.TenantId && x.MessageThreadId == request.ThreadId && memberIds.Contains(x.Id))
            .ToListAsync(ct)).ToDictionary(x => x.Id, x => x.CredentialId);

        return Result<GetMessageReceiptsResponse>.Success(new GetMessageReceiptsResponse
        {
            PageIndex = request.PageIndex, PageSize = request.PageSize, TotalCount = count,
            ReadReceiptsEnabled = allowReadReceipts,
            Items = rows.Where(x => credentials.ContainsKey(x.MessageThreadMemberId)).Select(x => new MessageReceiptItemResponse
            {
                MemberId = x.MessageThreadMemberId,
                CredentialId = credentials[x.MessageThreadMemberId],
                // One row per (member, message): marking read promotes the delivered row in place,
                // so its CreatedAt stays the moment of delivery and ModifiedAt becomes the read.
                DeliveredAt = x.CreatedAt,
                ReadAt = allowReadReceipts && x.TypeId == readTypeId ? x.ModifiedAt ?? x.CreatedAt : null
            }).ToList()
        });
    }
}
