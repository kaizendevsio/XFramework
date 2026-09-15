using System.Net;
using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts.Responses;

namespace Communications.Api.Services;

public sealed partial class ThreadService
{
    public async Task<Result<CmdResponse>> MarkMessagesDeliveredAsync(MarkMessagesDeliveredRequest request, CancellationToken ct = default)
    {
        try
        {
            if (request.ThreadId == Guid.Empty || request.MessageIds is not { Count: > 0 and <= 50 }
                || request.MessageIds.Contains(Guid.Empty))
                return Result<CmdResponse>.Failure("Choose between 1 and 50 message IDs in a conversation", 400);
            var callerResult = await ResolveCallerAsync(request.Metadata, ct);
            if (!callerResult.IsSuccess) return CallerFailure<CmdResponse>(callerResult);
            var caller = callerResult.Data!;
            var member = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.TenantId == caller.TenantId && m.MessageThreadId == request.ThreadId
                    && m.CredentialId == caller.CredentialId && !m.IsDeleted && m.IsEnabled)
                .FirstOrDefaultAsync(ct);
            if (member is null) return Result<CmdResponse>.Forbidden("Requester is not a member of this thread");

            var ids = request.MessageIds.Distinct().ToList();
            var messages = await dataContext.Query<Message>()
                .Where(m => m.TenantId == caller.TenantId && m.MessageThreadId == request.ThreadId
                    && ids.Contains(m.Id) && !m.IsDeleted && m.IsEnabled).ToListAsync(ct);
            if (messages.Count != ids.Count)
                return Result<CmdResponse>.NotFound("One or more messages were not found in this thread");
            foreach (var message in messages)
                if (!await CanAccessMessageAsync(caller.TenantId, member, message, ct))
                    return Result<CmdResponse>.NotFound("One or more messages were not found in this thread");

            ids = messages.Where(m => m.MessageThreadMemberId != member.Id && EncryptionReadyFor(m, member.Id))
                .Select(m => m.Id).ToList();
            await using var receiptLock = await ConversationMutationLock.AcquireReceiptsAsync(db, caller.TenantId, member.Id, ct);
            var existing = await dataContext.Query<MessageDelivery>()
                .Where(d => d.TenantId == caller.TenantId && d.MessageThreadMemberId == member.Id
                    && ids.Contains(d.MessageId) && !d.IsDeleted).ToListAsync(ct);
            var receivedIds = ids.Except(existing.Select(d => d.MessageId)).ToList();
            if (receivedIds.Count > 0)
            {
                var deliveredType = await ResolveDeliveryTypeIdAsync(caller.TenantId, MessageDeliveryTypes.Delivered, ct);
                if (deliveredType is null) return Result<CmdResponse>.NotFound("Initialize chat defaults before using chat");
                foreach (var id in receivedIds)
                    dataContext.Add(new MessageDelivery
                    {
                        Id = Guid.NewGuid(), TenantId = caller.TenantId, MessageThreadMemberId = member.Id,
                        MessageId = id, TypeId = deliveredType.Value, IsEnabled = true,
                        CreatedAt = DateTime.UtcNow, ConcurrencyStamp = Guid.NewGuid()
                    });
                AddOutboxEvent(MessageRealtimeEvents.MessagesDelivered, caller.TenantId, request.ThreadId,
                    member.Id, nameof(MessageDelivery), caller.CredentialId,
                    new { request.ThreadId, MessageIds = receivedIds });
                await SaveAndSignalAsync(ct);
            }
            return Result<CmdResponse>.Success(new CmdResponse
            { HttpStatusCode = HttpStatusCode.OK, Message = $"{receivedIds.Count} message(s) delivered" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error acknowledging delivery in thread {ThreadId}", request.ThreadId);
            return OperationFailure<CmdResponse>(ex, "Error acknowledging delivery");
        }
    }
}
