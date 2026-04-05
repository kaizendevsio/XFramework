using Messaging.Domain.Shared.Contracts.Requests.Delete;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Messaging.Api.Features.Messages.DeleteMessage;

public static class DeleteThreadMessageEndpoint
{
    [BoltHandler]
    [MapDelete("/api/threads/{threadId:guid}/messages/{messageId:guid}", Tags = ["Messages"],
        Summary = "Delete a message in a thread",
        Description = "Soft-deletes a message after verifying the requester is a thread member and message owner.")]
    public static async Task<Result<CmdResponse>> Handle(
        DeleteThreadMessageRequest request,
        IThreadService threadService,
        CancellationToken ct)
    {
        return await threadService.DeleteThreadMessageAsync(request, ct);
    }
}
