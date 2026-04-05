using FluentValidation;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Auth.ResetPassword;

public static class ResetPasswordEndpoint
{
    [BoltHandler]
    [MapPost("/api/auth/reset-password", Tags = ["Auth"],
        Summary = "Reset password with token",
        Description = "Resets a user's password using a valid reset token. Validates the token, hashes the new password, and invalidates the token.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result> Handle(
        ResetPasswordRequest request,
        IAuthService authService,
        CancellationToken ct)
    {
        return await authService.ResetPasswordAsync(request, ct);
    }
}

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Reset token is required");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters");
    }
}
