using FluentValidation;
using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Auth.ChangePassword;

public static class ChangePasswordEndpoint
{
    [BoltHandler]
    [MapPost("/api/auth/change-password", Tags = ["Auth"],
        Summary = "Change user password",
        Description = "Changes a user's password with optional verification requirement. Uses BCrypt hashing.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result> Handle(
        ChangePasswordRequest request,
        IAuthService authService,
        CancellationToken ct)
    {
        return await authService.ChangePasswordAsync(request, ct);
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
            .MinimumLength(8).WithMessage("Password must be at least 8 characters");

        When(x => x.RequireVerificationId, () =>
        {
            RuleFor(x => x.VerificationId)
                .NotEmpty().WithMessage("Verification ID is required when verification is required");
        });
    }
}
