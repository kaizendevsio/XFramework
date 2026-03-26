using Messaging.Api.Services;
using Messaging.Domain.Shared.Contracts.Requests.Attachments;
using XFramework.Core.Patterns;
using XFramework.Domain.Shared.Contracts;
using XFramework.Integration.Attributes;

namespace Messaging.Api.Features.Messages.Attachments.Create;

public static class CreateMessageFileEndpoint
{
    [BoltHandler]
    [MapPost("/api/threads/{threadId:guid}/messages/{messageId:guid}/files", Tags = ["Messages"],
        Summary = "Attach a file to a message",
        Description = "Creates a file attachment linking a message to a storage file.")]
    public static async Task<Result<CmdResponse>> Handle(
        CreateMessageFileRequest request,
        IThreadService threadService,
        CancellationToken ct)
    {
        return await threadService.CreateMessageFileAsync(request, ct);
    }
}
