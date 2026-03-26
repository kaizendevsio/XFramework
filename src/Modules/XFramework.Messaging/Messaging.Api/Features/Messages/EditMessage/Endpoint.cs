using Messaging.Api.Services;
using Messaging.Domain.Shared.Contracts.Requests.Edit;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts;
using XFramework.Integration.Attributes;

namespace Messaging.Api.Features.Messages.EditMessage;

public static class EditThreadMessageEndpoint
{
    [BoltHandler]
    [MapPatch("/api/threads/{threadId:guid}/messages/{messageId:guid}", Tags = ["Messages"],
        Summary = "Edit a message in a thread",
        Description = "Updates the text of a message after verifying the requester is a thread member and message owner.")]
    public static async Task<Result<CmdResponse>> Handle(
        EditThreadMessageRequest request,
        IThreadService threadService,
        CancellationToken ct)
    {
        return await threadService.EditThreadMessageAsync(request, ct);
    }
}
