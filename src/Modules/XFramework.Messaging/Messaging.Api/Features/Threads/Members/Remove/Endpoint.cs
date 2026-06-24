using Messaging.Domain.Shared.Contracts.Requests.Threads;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Messaging.Api.Features.Threads.Members.Remove;

public static class RemoveThreadMemberEndpoint
{
    [BoltHandler]
    [MapDelete("/api/threads/{threadId:guid}/members/{credentialId:guid}", Tags = ["Thread Members"],
        Summary = "Remove a member from a thread",
        Description = "Removes a credential from the specified thread. Cannot remove the last member.")]
    public static async Task<Result<CmdResponse>> Handle(
        RemoveThreadMemberRequest request,
        IThreadService threadService,
        CancellationToken ct)
    {
        return await threadService.RemoveThreadMemberAsync(request, ct);
    }
}
