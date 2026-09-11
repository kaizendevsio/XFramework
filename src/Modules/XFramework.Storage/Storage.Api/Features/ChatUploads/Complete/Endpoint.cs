using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Integration.Attributes;

namespace Storage.Api.Features.ChatUploads.Complete;

public static class CompleteChatStorageUploadSessionEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.StorageWrite],
        RequiredActorCapabilities = [StorageAuthorizationCapabilities.Create])]
    public static async Task<Result<StorageFileResponse>> Handle(
        CompleteChatStorageUploadSessionRequest request,
        ITrustedInvocationFeatureGate featureGate,
        StorageService storageService,
        CancellationToken ct)
    {
        var gate = await featureGate.EnsureAllowedAsync("/api/storage/chat-uploads", "POST",
            StorageAuthorizationCapabilities.CreateKey, ct);
        if (!gate.IsSuccess) return Result<StorageFileResponse>.Failure(gate.Message, gate.StatusCode);
        return await storageService.CompleteChatUploadAsync(request, ct);
    }
}
