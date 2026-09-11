using XFramework.Core.Patterns;
using XFramework.Core.Services.FeatureGates;
using XFramework.Integration.Attributes;

namespace Storage.Api.Features.ChatUploads.ValidateReference;

public static class ValidateChatStorageFileReferenceEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.StorageRead],
        RequiredActorCapabilities = [StorageAuthorizationCapabilities.Create],
        AllowedServiceCallers = [XFrameworkServiceNames.Communications])]
    public static async Task<Result<StorageFileValidationResponse>> Handle(
        ValidateChatStorageFileReferenceRequest request,
        ITrustedInvocationFeatureGate featureGate,
        StorageService storageService,
        CancellationToken ct)
    {
        var gate = await featureGate.EnsureAllowedAsync("/api/storage/chat-uploads", "POST",
            StorageAuthorizationCapabilities.CreateKey, ct);
        if (!gate.IsSuccess) return Result<StorageFileValidationResponse>.Failure(gate.Message, gate.StatusCode);
        return await storageService.ValidateChatFileReferenceAsync(request, ct);
    }
}
