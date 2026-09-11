using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Integration.Attributes;

namespace Storage.Api.Features.ChatUploads.UploadPart;

public static class UploadChatStorageFilePartEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.StorageWrite],
        RequiredActorCapabilities = [StorageAuthorizationCapabilities.Create])]
    public static async Task<Result<StorageUploadPartResponse>> Handle(
        UploadChatStorageFilePartRequest request,
        ITrustedInvocationFeatureGate featureGate,
        StorageService storageService,
        CancellationToken ct)
    {
        var gate = await featureGate.EnsureAllowedAsync("/api/storage/chat-uploads", "POST",
            StorageAuthorizationCapabilities.CreateKey, ct);
        if (!gate.IsSuccess) return Result<StorageUploadPartResponse>.Failure(gate.Message, gate.StatusCode);
        return await storageService.UploadChatPartAsync(request, ct);
    }
}
