using System.Net;
using Communications.Domain.Shared.Contracts.Requests.Threads;
using XFramework.Core.Patterns;

namespace Communications.Api.Services;

public sealed partial class ThreadService
{
    public async Task<Result<CmdResponse>> SetThreadActiveStatusAsync(SetThreadActiveStatusRequest request, CancellationToken ct = default)
    {
        if (request.ThreadId == Guid.Empty) return Result<CmdResponse>.Failure("Thread ID is required", 400);
        try
        {
            var callerResult = await ResolveCallerAsync(request.Metadata, ct);
            if (!callerResult.IsSuccess) return CallerFailure<CmdResponse>(callerResult);
            var caller = callerResult.Data!;
            var member = await GetActiveMemberAsync(caller.TenantId, request.ThreadId, caller.CredentialId, ct);
            if (member is null) return Result<CmdResponse>.Forbidden("Requester is not a member of this thread");
            member.HideActiveStatus = !request.ShareActiveStatus;
            member.ModifiedAt = DateTime.UtcNow;
            dataContext.Update(member);
            await SaveAndSignalAsync(ct);
            return Result<CmdResponse>.Success(new CmdResponse { HttpStatusCode = HttpStatusCode.OK });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating active status preference for thread {ThreadId}", request.ThreadId);
            return OperationFailure<CmdResponse>(ex, "Error updating active status preference");
        }
    }
}
