using FluentValidation;
using IdentityServer.Domain.Shared;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Credentials.Avatar.Upload;

public static class UploadCredentialAvatarEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin],
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = ["identity.tenants:manage"])]
    public static Task<Result<CredentialAvatarResponse>> Handle(
        UploadCredentialAvatarRequest request,
        IAuthService authService,
        CancellationToken ct) => authService.UploadCredentialAvatarAsync(request, ct);

    [MapPost("/api/credentials/avatar/upload", Tags = ["Credentials"],
        Summary = "Upload a credential avatar",
        Description = "Uploads an image avatar for an identity credential and stores only metadata on the credential.",
        RequireAuthorization = true,
        Capability = IdentityAuthorizationConstants.Update,
        ExcludeFromOpenApi = false)]
    public static Task<Result<CredentialAvatarResponse>> HandleHttp(
        UploadCredentialAvatarRequest request,
        HttpContext httpContext,
        IAuthService authService,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpDiagnostics(request.Metadata, httpContext);
        return authService.UploadCredentialAvatarAsync(request, ct);
    }
}

public class UploadCredentialAvatarRequestValidator : AbstractValidator<UploadCredentialAvatarRequest>
{
    public UploadCredentialAvatarRequestValidator()
    {
        RuleFor(x => x.CredentialId)
            .NotEmpty().WithMessage("Credential is required");

        RuleFor(x => x.FileBytes)
            .NotEmpty().WithMessage("Avatar image is required")
            .Must(bytes => bytes is null || bytes.Length <= CredentialAvatarPolicy.MaxFileSizeBytes)
            .WithMessage("Avatar image must be 5 MB or smaller");

        RuleFor(x => x.ContentType)
            .Must(CredentialAvatarPolicy.IsAllowedContentType)
            .WithMessage("Avatar image must be PNG, JPEG, or WebP");
    }
}
