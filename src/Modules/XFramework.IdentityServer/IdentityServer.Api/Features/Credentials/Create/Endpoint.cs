using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using IdentityServer.Api.Infrastructure;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Credentials.Create;

public static class CreateCredentialEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin],
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = ["identity.tenants:manage"])]
    public static Task<Result<CredentialAdministrationResponse>> Handle(
        CreateCredentialRequest request,
        IAuthService authService,
        CancellationToken ct) => authService.CreateCredentialAsync(request, ct);

    [MapPost("/api/credentials", Tags = ["Credentials"],
        Summary = "Create a new identity credential",
        Description = "Creates a new identity credential with BCrypt password hashing (workFactor 11).",
        RequireAuthorization = true,
        Capability = IdentityAuthorizationConstants.Create)]
    public static Task<Result<CredentialAdministrationResponse>> HandleHttp(
        CreateCredentialRequest request,
        HttpContext httpContext,
        IAuthService authService,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpDiagnostics(request.Metadata, httpContext);
        return authService.CreateCredentialAsync(request, ct);
    }
}

public class CreateCredentialRequestValidator : AbstractValidator<CreateCredentialRequest>
{
    public CreateCredentialRequestValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("Username is required")
            .MaximumLength(256).WithMessage("Username must not exceed 256 characters");

        RuleFor(x => x.UserAlias)
            .MaximumLength(256).WithMessage("User alias must not exceed 256 characters");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Must(IdentityPasswordPolicy.IsWithinBcryptByteLimit)
            .WithMessage("Password must not exceed 72 UTF-8 bytes");

        RuleFor(x => x.IdentityInfoId)
            .NotEmpty().WithMessage("Identity Info ID is required");
    }
}
