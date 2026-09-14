using System.Security.Cryptography;
using System.Text;
using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts.Responses;

namespace Communications.Api.Services;

public sealed partial class ThreadService
{
    private static string EnvelopeHash(string envelope) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(envelope)));

    public async Task<Result<DeferredEncryptionResponse>> GetDeferredEncryptionAsync(GetDeferredEncryptionRequest request, CancellationToken ct = default)
    {
        try
        {
            var resolved = await ResolveCallerAsync(request.Metadata, ct);
            if (!resolved.IsSuccess) return CallerFailure<DeferredEncryptionResponse>(resolved);
            var caller = resolved.Data!;
            var memberships = await dataContext.Query<MessageThreadMember>().Where(m => m.TenantId == caller.TenantId
                && m.CredentialId == caller.CredentialId && !m.IsDeleted && m.IsEnabled).ToListAsync(ct);
            var threadIdsForActor = memberships.Select(m => m.MessageThreadId).ToArray();
            var activeThreads = await dataContext.Query<MessageThread>().Where(t => t.TenantId == caller.TenantId
                && threadIdsForActor.Contains(t.Id) && !t.IsDeleted && t.IsEnabled).ToListAsync(ct);
            var activeThreadIds = activeThreads.Select(t => t.Id).ToHashSet();
            var ownMemberIds = memberships.Where(m => activeThreadIds.Contains(m.MessageThreadId)).Select(m => m.Id).ToArray();
            var query = dataContext.Query<Message>().NoCache().Where(m => m.TenantId == caller.TenantId
                && ownMemberIds.Contains(m.MessageThreadMemberId) && !m.IsDeleted && m.IsEnabled
                && m.PendingEncryptionCount > 0 && m.EncryptedEnvelope != null);
            var total = await query.CountAsync(ct);
            var messages = await query.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
                .Skip(Math.Clamp(request.PageIndex, 0, 100000) * 20).Take(20).ToListAsync(ct);
            var threadIds = messages.Select(m => m.MessageThreadId).Distinct().ToArray();
            var members = await dataContext.Query<MessageThreadMember>().Where(m => m.TenantId == caller.TenantId
                && threadIds.Contains(m.MessageThreadId) && !m.IsDeleted && m.IsEnabled).ToListAsync(ct);
            return Result<DeferredEncryptionResponse>.Success(new()
            {
                TotalCount = total,
                Items = messages.Select(m =>
                {
                    var audience = EncryptionMemberIds(m.EncryptionAudienceJson);
                    var pending = EncryptionMemberIds(m.PendingEncryptionMembersJson);
                    var current = members.Where(x => x.MessageThreadId == m.MessageThreadId && audience.Contains(x.Id)).ToList();
                    return new DeferredEncryptedMessage
                    {
                        ThreadId = m.MessageThreadId, EnvelopeHash = EnvelopeHash(m.EncryptedEnvelope!),
                        PendingCredentialIds = current.Where(x => pending.Contains(x.Id)).Select(x => x.CredentialId).ToList(),
                        Message = new ThreadMessageItemResponse
                        {
                            Id = m.Id, SenderCredentialId = caller.CredentialId, CreatedAt = m.CreatedAt,
                            Text = EncryptedMessages.Preview, EncryptedEnvelope = m.EncryptedEnvelope,
                            ParentMessageId = m.ParentMessageId, IsThreadReply = m.IsThreadReply,
                            EncryptionSenderDeviceId = m.EncryptionSenderDeviceId, AcceptedSenderDirectoryRevision = m.AcceptedSenderDirectoryRevision,
                            PendingEncryptionCount = m.PendingEncryptionCount,
                            EncryptionAudienceCredentialIds = current.Select(x => x.CredentialId).ToList()
                        }
                    };
                }).ToList()
            });
        }
        catch (Exception ex) { return OperationFailure<DeferredEncryptionResponse>(ex, "Error loading pending encrypted delivery"); }
    }

    public async Task<Result<CmdResponse>> CompleteDeferredEncryptionAsync(CompleteDeferredEncryptionRequest request, CancellationToken ct = default)
    {
        try
        {
            if (!EncryptedMessages.ValidEnvelope(request.EncryptedEnvelope) || request.ExpectedEnvelopeHash?.Length != 64)
                return Result<CmdResponse>.Failure("Invalid encrypted delivery", 400);
            var resolved = await ResolveCallerAsync(request.Metadata, ct);
            if (!resolved.IsSuccess) return CallerFailure<CmdResponse>(resolved);
            var caller = resolved.Data!;
            await using var mutationLock = await ConversationMutationLock.AcquireAsync(db, caller.TenantId, request.ThreadId, ct);
            var members = await dataContext.Query<MessageThreadMember>().Where(m => m.TenantId == caller.TenantId
                && m.MessageThreadId == request.ThreadId && !m.IsDeleted && m.IsEnabled).ToListAsync(ct);
            var own = members.SingleOrDefault(m => m.CredentialId == caller.CredentialId);
            if (own is null) return Result<CmdResponse>.Forbidden("Conversation membership is required");
            var thread = await dataContext.Query<MessageThread>().Where(t => t.Id == request.ThreadId
                && t.TenantId == caller.TenantId && !t.IsDeleted && t.IsEnabled).FirstOrDefaultAsync(ct);
            if (thread is null) return Result<CmdResponse>.NotFound("Conversation not found");
            var message = await dataContext.Query<Message>().NoCache().Where(m => m.Id == request.MessageId
                && m.MessageThreadId == request.ThreadId && m.TenantId == caller.TenantId && !m.IsDeleted && m.IsEnabled).FirstOrDefaultAsync(ct);
            if (message is null) return Result<CmdResponse>.NotFound("Message not found");
            if (message.MessageThreadMemberId != own.Id) return Result<CmdResponse>.Forbidden("Only the sender can complete encrypted delivery");
            if (message.EncryptedEnvelope == request.EncryptedEnvelope && message.EncryptionSenderDeviceId == request.EncryptionSenderDeviceId
                && message.AcceptedSenderDirectoryRevision == request.SenderDirectoryRevision)
                return Result<CmdResponse>.Success(new() { HttpStatusCode = System.Net.HttpStatusCode.OK }); // Accepted retry, including after a lost response.
            if (message.EncryptedEnvelope is null || message.PendingEncryptionCount == 0
                || EnvelopeHash(message.EncryptedEnvelope) != request.ExpectedEnvelopeHash)
                return Result<CmdResponse>.Failure("Message changed. Refresh pending delivery.", 409);
            var original = EncryptionMemberIds(message.EncryptionAudienceJson);
            var audience = members.Where(m => original.Contains(m.Id)).ToList();
            if (members.Count == 2 && await IsBlockedAsync(caller.TenantId, caller.CredentialId,
                members.Single(m => m.Id != own.Id).CredentialId, ct))
                return Result<CmdResponse>.Forbidden("Direct communications is blocked");
            if (!await ReadyEncryptionRecipientsCurrentAsync(caller.TenantId, caller.CredentialId, request.EncryptionSenderDeviceId,
                request.SenderDirectoryRevision, request.RecipientDirectoryRevisions, audience.Select(m => m.CredentialId).ToArray(), ct))
                return Result<CmdResponse>.Failure("Encryption recipients changed. Refresh encryption.", 412);
            message.EncryptedEnvelope = request.EncryptedEnvelope;
            message.EncryptionSenderDeviceId = request.EncryptionSenderDeviceId;
            message.AcceptedSenderDirectoryRevision = request.SenderDirectoryRevision;
            SetPendingEncryption(message, audience, request.RecipientDirectoryRevisions.Keys);
            message.ModifiedAt = DateTime.UtcNow;
            dataContext.Update(message);
            AddOutboxEvent(MessageRealtimeEvents.MessageEdited, caller.TenantId, thread.Id, message.Id,
                nameof(Message), caller.CredentialId, new { ThreadId = thread.Id, MessageId = message.Id });
            await SaveAndSignalAsync(ct);
            return Result<CmdResponse>.Success(new() { HttpStatusCode = System.Net.HttpStatusCode.OK });
        }
        catch (Exception ex) { return OperationFailure<CmdResponse>(ex, "Error completing encrypted delivery"); }
    }
}
