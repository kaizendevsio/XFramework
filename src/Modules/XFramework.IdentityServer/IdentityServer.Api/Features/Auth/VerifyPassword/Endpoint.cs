using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using IdentityServer.Api.Infrastructure;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Auth.VerifyPassword;

public static class VerifyPasswordEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin],
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = ["identity.tenants:manage"])]
    public static Task<Result<bool>> Handle(
        VerifyPasswordRequest request,
        IAuthService authService,
        CancellationToken ct) => authService.VerifyPasswordAsync(request, ct);

    [MapPost("/api/auth/verify-password", Tags = ["Auth"],
        Summary = "Verify user password",
        Description = "Verifies a password against stored credential using BCrypt.",
        RequireAuthorization = true,
        ExcludeFromOpenApi = false)]
    public static Task<Result<bool>> HandleHttp(
        VerifyPasswordRequest request,
        HttpContext httpContext,
        IAuthService authService,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpDiagnostics(request.Metadata, httpContext);
        return authService.VerifyPasswordAsync(request, ct);
    }
}

public class VerifyPasswordRequestValidator : AbstractValidator<VerifyPasswordRequest>
{
    public VerifyPasswordRequestValidator()
    {
        RuleFor(x => x.CredentialId)
            .NotEmpty().WithMessage("Credential ID is required");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .Must(IdentityPasswordPolicy.IsWithinBcryptByteLimit)
            .WithMessage("Password must not exceed 72 UTF-8 bytes");
    }
}
