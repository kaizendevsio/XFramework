using FluentValidation;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace IdentityServer.Api.Features.Auth.ForgotPassword;

public static class ForgotPasswordEndpoint
{
    [BoltHandler(
        ActorRequirement = ActorRequirement.Optional,
        TenantAccessMode = TenantAccessMode.PublicTenantLookup,
        AllowAnonymous = true)]
    [MapPost("/api/auth/forgot-password", Tags = ["Auth"],
        Summary = "Request a password reset",
        Description = "Initiates a password reset flow by generating a reset token and sending it via email or SMS. Does not reveal if the account exists.",
        ExcludeFromOpenApi = false)]
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
            .Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.Phone))
            .WithMessage("Either email or phone is required");

        When(x => !string.IsNullOrEmpty(x.Email), () =>
        {
            RuleFor(x => x.Email)
                .MaximumLength(320).WithMessage("Email must not exceed 320 characters")
                .EmailAddress().WithMessage("A valid email address is required");
        });

        When(x => !string.IsNullOrEmpty(x.Phone), () =>
        {
            RuleFor(x => x.Phone)
                .NotEmpty().WithMessage("A valid phone number is required")
                .MaximumLength(64).WithMessage("Phone must not exceed 64 characters");
        });
    }
}
