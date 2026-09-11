using Communications.Domain.Shared.Contracts.Requests.Attachments;
using Storage.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Messages.Attachments.CreateUpload;

public static class CreateChatAttachmentUploadEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat])]
    [MapPost("/api/communications/threads/{threadId:guid}/attachments/uploads", Tags = ["Messages"],
        Capability = "create", Summary = "Start an actor-owned chat attachment upload")]
    public static Task<Result<StorageUploadSessionResponse>> Handle(
        CreateChatAttachmentUploadRequest request, IThreadService threadService, CancellationToken ct) =>
        threadService.CreateChatAttachmentUploadAsync(request, ct);
}
