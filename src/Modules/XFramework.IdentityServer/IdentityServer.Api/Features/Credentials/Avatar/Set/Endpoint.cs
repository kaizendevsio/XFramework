using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Credentials.Avatar.Set;

public static class SetCredentialAvatarEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin],
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = ["identity.tenants:manage"])]
    public static Task<Result<CredentialAvatarResponse>> Handle(
        SetCredentialAvatarRequest request,
        IAuthService authService,
        CancellationToken ct) => authService.SetCredentialAvatarAsync(request, ct);

    [MapPost("/api/credentials/avatar/set", Tags = ["Credentials"],
        Summary = "Set a credential avatar",
        Description = "Attaches an existing image storage file as the credential avatar.",
        RequireAuthorization = true,
        Capability = IdentityAuthorizationConstants.Update,
        ExcludeFromOpenApi = false)]
    public static Task<Result<CredentialAvatarResponse>> HandleHttp(
        SetCredentialAvatarRequest request,
        HttpContext httpContext,
        IAuthService authService,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpDiagnostics(request.Metadata, httpContext);
        return authService.SetCredentialAvatarAsync(request, ct);
    }
}

public class SetCredentialAvatarRequestValidator : AbstractValidator<SetCredentialAvatarRequest>
{
    public SetCredentialAvatarRequestValidator()
    {
        RuleFor(x => x.CredentialId)
            .NotEmpty().WithMessage("Credential is required");

        RuleFor(x => x.StorageFileId)
            .NotEmpty().WithMessage("Storage file is required");
    }
}
