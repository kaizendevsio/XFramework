namespace XFramework.Integration.Security;

public sealed class ServiceIdentityProvider(IServiceTokenValidator tokenValidator)
    : IServiceIdentityProvider
{
    public async Task<ServiceIdentityValidationResult> ValidateAsync(
        string token,
        string expectedAudience,
        CancellationToken ct = default)
    {
        var validation = await tokenValidator.ValidateAsync(token, expectedAudience, requiredScopes: null, ct);
        if (!validation.IsValid)
        {
            return ServiceIdentityValidationResult.Failure(
                validation.Error ?? "Service token is invalid.",
                validation.FailureStatusCode);
        }

        var generationId = validation.Principal?.FindFirst("client_credential_generation")?.Value;
        return ServiceIdentityValidationResult.Success(new TrustedServiceIdentity(
            validation.CallerClientId!,
            validation.Audience!,
            validation.Scopes,
            generationId));
    }
}

internal sealed class RejectingActorIdentityProvider : IActorIdentityProvider
{
    public Task<ActorIdentityValidationResult> ValidateAsync(string token, CancellationToken ct = default) =>
        Task.FromResult(ActorIdentityValidationResult.Failure(
            "Actor token validation is not configured for this service.",
            503));
}
