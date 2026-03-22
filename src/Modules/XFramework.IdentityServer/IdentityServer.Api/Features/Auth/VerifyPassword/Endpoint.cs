using FluentValidation;
using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Auth.VerifyPassword;

public static class VerifyPasswordEndpoint
{
    [StreamFlowHandler]
    [MapPost("/api/auth/verify-password", Tags = ["Auth"],
        Summary = "Verify user password",
        Description = "Verifies a password against stored credential using BCrypt.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<bool>> Handle(
        VerifyPasswordRequest request,
        IAuthService authService,
        CancellationToken ct)
    {
        return await authService.VerifyPasswordAsync(request, ct);
    }
}

public class VerifyPasswordRequestValidator : AbstractValidator<VerifyPasswordRequest>
{
    public VerifyPasswordRequestValidator()
    {
        RuleFor(x => x.CredentialId)
            .NotEmpty().WithMessage("Credential ID is required");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required");
    }
}
