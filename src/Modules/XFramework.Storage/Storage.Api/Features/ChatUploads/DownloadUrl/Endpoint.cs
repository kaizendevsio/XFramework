using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Integration.Attributes;

namespace Storage.Api.Features.ChatUploads.DownloadUrl;

public static class GetChatStorageDownloadUrlEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.StorageRead],
        RequiredActorCapabilities = [StorageAuthorizationCapabilities.View],
        AllowedServiceCallers = [XFrameworkServiceNames.Communications])]
    public static async Task<Result<StorageDownloadUrlResponse>> Handle(
        GetChatStorageDownloadUrlRequest request,
        ITrustedInvocationFeatureGate featureGate,
        StorageService storageService,
        CancellationToken ct)
    {
        var gate = await featureGate.EnsureAllowedAsync("/api/storage/chat-uploads", "GET",
            StorageAuthorizationCapabilities.ViewKey, ct);
        if (!gate.IsSuccess) return Result<StorageDownloadUrlResponse>.Failure(gate.Message, gate.StatusCode);
        return await storageService.GetChatDownloadUrlAsync(request, ct);
    }
}
