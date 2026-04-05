using Messaging.Domain.Shared.Contracts.Requests.Attachments;
using Messaging.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Messaging.Api.Features.Messages.Attachments.GetList;

public static class GetMessageFilesEndpoint
{
    [BoltHandler]
    [MapGet("/api/threads/{threadId:guid}/messages/{messageId:guid}/files", Tags = ["Messages"],
        Summary = "List files attached to a message",
        Description = "Returns all file attachments for a given message in a thread.")]
    public static async Task<Result<List<MessageFileResponse>>> Handle(
        GetMessageFilesRequest request,
        IThreadService threadService,
        CancellationToken ct)
    {
        return await threadService.GetMessageFilesAsync(request, ct);
    }
}
