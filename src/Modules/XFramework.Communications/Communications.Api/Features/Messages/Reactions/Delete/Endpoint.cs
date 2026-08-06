using Communications.Domain.Shared.Contracts.Requests.Reactions;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Messages.Reactions.Delete;

public static class DeleteMessageReactionEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat])]
    [MapDelete("/api/communications/threads/{threadId:guid}/messages/{messageId:guid}/reactions/{reactionId:guid}", Tags = ["Messages"],
        Summary = "Remove a reaction from a message",
        Description = "Soft-deletes a reaction after verifying the requester is a member of the thread.")]
    public static async Task<Result<CmdResponse>> Handle(
        DeleteMessageReactionRequest request,
        IThreadService threadService,
        CancellationToken ct)
    {
        return await threadService.DeleteMessageReactionAsync(request, ct);
    }
}
