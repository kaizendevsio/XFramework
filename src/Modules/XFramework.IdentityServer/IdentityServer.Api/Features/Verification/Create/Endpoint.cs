using FluentValidation;
using XFramework.Integration.Attributes;
using CreateVerificationRequest = XFramework.Domain.Shared.Contracts.Requests.Create<IdentityServer.Domain.Shared.Contracts.IdentityVerification>;

namespace IdentityServer.Api.Features.Verification.Create;

public static class CreateVerificationEndpoint
{
    [MapPost("/api/verifications", Tags = ["Verification"],
        Summary = "Create a new verification",
        Description = "Creates a verification code (SMS OTP) for multi-factor authentication and sends SMS message.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<IdentityVerification>> Handle(
        CreateVerificationRequest request,
        IAuthService authService,
        CancellationToken ct)
    {
        return await authService.CreateVerificationAsync(request, ct);
    }
}

public class CreateVerificationRequestValidator : AbstractValidator<CreateVerificationRequest>
{
    public CreateVerificationRequestValidator()
    {
        RuleFor(x => x.Model.CredentialId)
            .NotEmpty().WithMessage("Credential ID is required");

        RuleFor(x => x.Model.VerificationTypeId)
            .NotEmpty().WithMessage("Verification Type ID is required");
    }
}
