using Communications.Domain.Shared.Contracts.Requests.Reactions;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Messages.Reactions.Create;

public static class CreateMessageReactionEndpoint
{
    [BoltHandler]
    [MapPost("/api/communications/threads/{threadId:guid}/messages/{messageId:guid}/reactions", Tags = ["Messages"],
        Summary = "Add a reaction to a message",
        Description = "Creates a reaction on a message, preventing duplicates of the same type.")]
    public static async Task<Result<CmdResponse>> Handle(
        CreateMessageReactionRequest request,
        IThreadService threadService,
        CancellationToken ct)
    {
        return await threadService.CreateMessageReactionAsync(request, ct);
    }
}
