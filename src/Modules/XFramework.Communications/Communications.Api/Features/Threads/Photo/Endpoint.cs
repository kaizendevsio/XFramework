using Communications.Domain.Shared.Contracts.Requests.Attachments;
using Storage.Domain.Shared.Contracts.Responses;
using XFramework.Integration.Attributes;
using XFramework.Core.Patterns;

namespace Communications.Api.Features.Threads.Photo;

public static class GetThreadPhotoDownloadUrlEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.CommunicationsChat])]
    public static Task<Result<StorageDownloadUrlResponse>> Handle(
        GetThreadPhotoDownloadUrlRequest request, IThreadService service, CancellationToken ct) =>
        service.GetThreadPhotoDownloadUrlAsync(request, ct);
}
