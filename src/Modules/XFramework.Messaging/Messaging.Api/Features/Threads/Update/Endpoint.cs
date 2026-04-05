using Messaging.Domain.Shared.Contracts.Requests.Threads;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Messaging.Api.Features.Threads.Update;

public static class UpdateThreadEndpoint
{
    [BoltHandler]
    [MapPatch("/api/threads/{threadId:guid}", Tags = ["Threads"],
        Summary = "Update a thread",
        Description = "Updates thread name and/or description. Validates the requester is a member.")]
    public static async Task<Result<CmdResponse>> Handle(
        UpdateThreadRequest request,
        IThreadService threadService,
        CancellationToken ct)
    {
        return await threadService.UpdateThreadAsync(request, ct);
    }
}
