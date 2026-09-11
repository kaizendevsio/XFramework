using Communications.Domain.Shared.Contracts.Requests.Attachments;
using Storage.Domain.Shared.Contracts.Responses;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace Communications.Api.Features.Messages.Attachments.DownloadUrl;

public static class GetChatAttachmentDownloadUrlEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat])]
    [MapPost("/api/communications/threads/{threadId:guid}/messages/{messageId:guid}/files/{fileId:guid}/download-url",
        Tags = ["Messages"], Capability = "view", Summary = "Download a visible linked chat attachment")]
    public static Task<Result<StorageDownloadUrlResponse>> Handle(
        GetChatAttachmentDownloadUrlRequest request, IThreadService threadService, CancellationToken ct) =>
        threadService.GetChatAttachmentDownloadUrlAsync(request, ct);
}
