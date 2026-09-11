using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Integration.Attributes;

namespace Storage.Api.Features.ChatUploads.Create;

public static class CreateChatStorageUploadSessionEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.StorageWrite],
        RequiredActorCapabilities = [StorageAuthorizationCapabilities.Create],
        AllowedServiceCallers = [XFrameworkServiceNames.Communications])]
    public static async Task<Result<StorageUploadSessionResponse>> Handle(
        CreateChatStorageUploadSessionRequest request,
        ITrustedInvocationFeatureGate featureGate,
        StorageService storageService,
        CancellationToken ct)
    {
        var gate = await featureGate.EnsureAllowedAsync("/api/storage/chat-uploads", "POST",
            StorageAuthorizationCapabilities.CreateKey, ct);
        if (!gate.IsSuccess) return Result<StorageUploadSessionResponse>.Failure(gate.Message, gate.StatusCode);
        return await storageService.CreateChatUploadSessionAsync(request, ct);
    }
}
