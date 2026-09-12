using System.Net;
using Communications.Domain.Shared;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using Communications.Domain.Shared.Contracts.Responses;
using XFramework.Domain.Shared.DataContext;
using XFramework.Core.Patterns;

namespace Communications.Api.Services;

public sealed partial class ThreadService
{
    public async Task<Result<GetDeletedThreadsResponse>> GetDeletedThreadsAsync(GetDeletedThreadsRequest request, CancellationToken ct = default)
    {
        try
        {
            var resolved = await ResolveCallerAsync(request.Metadata, ct);
            if (!resolved.IsSuccess) return CallerFailure<GetDeletedThreadsResponse>(resolved);
            var caller = resolved.Data!;
            var page = Math.Max(0, request.PageIndex); var size = Math.Clamp(request.PageSize, 1, 100);
            // Explicit tombstone read: only this tenant and this credential's old memberships.
            var query = dataContext.Query<MessageThreadMember>().IgnoreQueryFilters()
                .Where(m => m.TenantId == caller.TenantId && m.CredentialId == caller.CredentialId && m.IsDeleted && m.IsArchived);
            var count = await query.CountAsync(ct);
            var members = await query.OrderBy(m => m.Id).Skip(page * size).Take(size).ToListAsync(ct);
            var ids = members.Select(m => m.MessageThreadId).Distinct().ToList();
            var threads = await dataContext.Query<MessageThread>().IgnoreQueryFilters()
                .Where(t => t.TenantId == caller.TenantId && ids.Contains(t.Id) && t.IsDeleted).ToListAsync(ct);
            return Result<GetDeletedThreadsResponse>.Success(new() { Items = threads.Select(t => t.Id).ToList(), TotalCount = count });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading deleted conversations");
            return OperationFailure<GetDeletedThreadsResponse>(ex, "Could not synchronize deleted conversations");
        }
    }

    public async Task<Result<CmdResponse>> DeleteThreadAsync(DeleteThreadRequest request, CancellationToken ct = default)
    {
        try
        {
            var resolved = await ResolveCallerAsync(request.Metadata, ct);
            if (!resolved.IsSuccess) return CallerFailure<CmdResponse>(resolved);
            var caller = resolved.Data!;
            await using var mutationLock = await ConversationMutationLock.AcquireAsync(db, caller.TenantId, request.ThreadId, ct);
            var thread = await dataContext.Query<MessageThread>()
                .Where(t => t.TenantId == caller.TenantId && t.Id == request.ThreadId && !t.IsDeleted && t.IsEnabled).FirstOrDefaultAsync(ct);
            if (thread is null) return Result<CmdResponse>.NotFound("Conversation not found");
            var actor = await GetActiveMemberAsync(caller.TenantId, thread.Id, caller.CredentialId, ct);
            if (actor is null || !await CanManageThreadAsync(actor, ct))
                return Result<CmdResponse>.Forbidden("Only conversation admins can delete a conversation for everyone");

            var now = DateTime.UtcNow;
            // Revoke memberships to block message, search, attachment and realtime access.
            // Retain tombstones so offline members can remove their cached copy on reconnect.
            var members = await dataContext.Query<MessageThreadMember>()
                .Where(m => m.TenantId == caller.TenantId && m.MessageThreadId == thread.Id && !m.IsDeleted && m.IsEnabled).ToListAsync(ct);
            foreach (var member in members)
            {
                member.IsDeleted = true; member.IsEnabled = false; member.DeletedAt = now;
                member.IsArchived = true; member.ArchivedAt = now; dataContext.Update(member);
            }
            var direct = await dataContext.Query<MessageDirectThread>()
                .Where(d => d.TenantId == caller.TenantId && d.MessageThreadId == thread.Id && !d.IsDeleted && d.IsEnabled).FirstOrDefaultAsync(ct);
            if (direct is not null) { direct.IsDeleted = true; direct.IsEnabled = false; direct.DeletedAt = now; dataContext.Update(direct); }
            thread.IsDeleted = true; thread.IsEnabled = false; thread.DeletedAt = now; dataContext.Update(thread);
            AddOutboxEvent(MessageRealtimeEvents.ThreadDeleted, caller.TenantId, thread.Id, thread.Id, nameof(MessageThread),
                caller.CredentialId, new { ThreadId = thread.Id, RecipientCredentialIds = members.Select(m => m.CredentialId).Distinct().ToArray() });
            await SaveAndSignalAsync(ct);
            return Result<CmdResponse>.Success(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting conversation {ThreadId}", request.ThreadId);
            return OperationFailure<CmdResponse>(ex, "Could not delete conversation");
        }
    }
}
