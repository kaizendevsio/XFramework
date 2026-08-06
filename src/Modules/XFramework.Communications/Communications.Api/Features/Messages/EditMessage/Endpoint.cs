using Communications.Domain.Shared.Contracts.Requests.Edit;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Messages.EditMessage;

public static class EditThreadMessageEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat])]
    [MapPatch("/api/communications/threads/{threadId:guid}/messages/{messageId:guid}", Tags = ["Messages"],
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
