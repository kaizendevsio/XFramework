using FluentValidation;
using IdentityServer.Api.Services;
using IdentityServer.Domain.Shared.Contracts.Requests;
using XFramework.Core.Patterns;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Auth.ForgotPassword;

public static class ForgotPasswordEndpoint
{
    [BoltHandler]
    [MapPost("/api/auth/forgot-password", Tags = ["Auth"],
        Summary = "Request a password reset",
        Description = "Initiates a password reset flow by generating a reset token and sending it via email or SMS. Does not reveal if the account exists.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result> Handle(
        ForgotPasswordRequest request,
        IAuthService authService,
        CancellationToken ct)
    {
        return await authService.ForgotPasswordAsync(request, ct);
    }
}

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x)
            .Must(x => !string.IsNullOrEmpty(x.Email) || !string.IsNullOrEmpty(x.Phone))
            .WithMessage("Either email or phone is required");

        When(x => !string.IsNullOrEmpty(x.Email), () =>
        {
            RuleFor(x => x.Email)
                .EmailAddress().WithMessage("A valid email address is required");
        });

        When(x => !string.IsNullOrEmpty(x.Phone), () =>
        {
            RuleFor(x => x.Phone)
                .NotEmpty().WithMessage("A valid phone number is required");
        });
    }
}
