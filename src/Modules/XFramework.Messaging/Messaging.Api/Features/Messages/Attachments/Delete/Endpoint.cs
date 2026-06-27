using Messaging.Domain.Shared.Contracts.Requests.Attachments;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Messaging.Api.Features.Messages.Attachments.Delete;

public static class DeleteMessageFileEndpoint
{
    [BoltHandler]
    [MapDelete("/api/threads/{threadId:guid}/messages/{messageId:guid}/files/{fileId:guid}", Tags = ["Messages"],
        Summary = "Detach a file from a message",
        Description = "Soft-deletes the Messaging attachment link. The Storage file remains owned by the Storage module.")]
    public static async Task<Result<CmdResponse>> Handle(
        DeleteMessageFileRequest request,
        IThreadService threadService,
        CancellationToken ct)
    {
        return await threadService.DeleteMessageFileAsync(request, ct);
    }
}
