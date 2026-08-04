using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using IdentityServer.Api.Features.Verification;
using XFramework.Integration.Attributes;
using CreateVerificationRequest = XFramework.Domain.Shared.Contracts.Requests.Create<IdentityServer.Domain.Shared.Contracts.IdentityVerification>;

namespace IdentityServer.Api.Features.Verification.Create;

public static class CreateVerificationEndpoint
{
    [MapPost("/api/verifications", Tags = ["Verification"],
        Summary = "Create a new verification",
        Description = "Creates a verification code (SMS OTP) for multi-factor authentication and sends SMS message.",
        RequireAuthorization = true)]
    public static async Task<Result<VerificationAdministrationResponse>> Handle(
        CreateVerificationRequest request,
        HttpContext httpContext,
        IAuthService authService,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpDiagnostics(request.Metadata, httpContext);
        return VerificationResponseMapper.Map(
            await authService.CreateVerificationAsync(request, ct));
    }
}

public class CreateVerificationRequestValidator : AbstractValidator<CreateVerificationRequest>
{
    public CreateVerificationRequestValidator()
    {
        RuleFor(x => x.Model)
            .NotNull().WithMessage("Verification model is required");

        When(x => x.Model is not null, () =>
        {
            RuleFor(x => x.Model.CredentialId)
                .NotEmpty().WithMessage("Credential ID is required");

            RuleFor(x => x.Model.VerificationTypeId)
                .NotEmpty().WithMessage("Verification Type ID is required");
        });
    }
}
