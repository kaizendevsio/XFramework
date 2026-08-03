using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using IdentityServer.Api.Infrastructure;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Auth.ChangePassword;

public static class ChangePasswordEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin])]
    public static Task<Result> Handle(
        ChangePasswordRequest request,
        IAuthService authService,
        CancellationToken ct) => authService.ChangePasswordAsync(request, ct);

    [MapPost("/api/auth/change-password", Tags = ["Auth"],
        Summary = "Change user password",
        Description = "Changes a user's password after an approved verification challenge. Uses BCrypt hashing.",
        RequireAuthorization = true,
        ExcludeFromOpenApi = false)]
    public static Task<Result> HandleHttp(
        ChangePasswordRequest request,
        HttpContext httpContext,
        IAuthService authService,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpContextActor(request.Metadata, httpContext);
        return authService.ChangePasswordAsync(request, ct);
    }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CreadentialId)
            .NotEmpty().WithMessage("Credential ID is required");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters")
            .Must(IdentityPasswordPolicy.IsWithinBcryptByteLimit)
            .WithMessage("Password must not exceed 72 UTF-8 bytes");

        RuleFor(x => x.VerificationId)
            .NotEmpty().WithMessage("Verification ID is required");
    }
}
