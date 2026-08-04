using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Credentials.Avatar.Remove;

public static class RemoveCredentialAvatarEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin],
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = ["identity.tenants:manage"])]
    public static Task<Result<CredentialAvatarResponse>> Handle(
        RemoveCredentialAvatarRequest request,
        IAuthService authService,
        CancellationToken ct) => authService.RemoveCredentialAvatarAsync(request, ct);

    [MapPost("/api/credentials/avatar/remove", Tags = ["Credentials"],
        Summary = "Remove a credential avatar",
        Description = "Clears credential avatar metadata without deleting the stored file.",
        RequireAuthorization = true,
        Capability = IdentityAuthorizationConstants.Update,
        ExcludeFromOpenApi = false)]
    public static Task<Result<CredentialAvatarResponse>> HandleHttp(
        RemoveCredentialAvatarRequest request,
        HttpContext httpContext,
        IAuthService authService,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpDiagnostics(request.Metadata, httpContext);
        return authService.RemoveCredentialAvatarAsync(request, ct);
    }
}

public class RemoveCredentialAvatarRequestValidator : AbstractValidator<RemoveCredentialAvatarRequest>
{
    public RemoveCredentialAvatarRequestValidator()
    {
        RuleFor(x => x.CredentialId)
            .NotEmpty().WithMessage("Credential is required");
    }
}
