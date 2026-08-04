using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using IdentityServer.Domain.Shared.Contracts.Responses;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace IdentityServer.Api.Features.Verification.Check;

public static class CheckVerificationEndpoint
{
    [BoltHandler(
        ActorRequirement = ActorRequirement.Optional,
        TenantAccessMode = TenantAccessMode.ServiceTargetTenant,
        RequiredServiceScopes = [XFrameworkServiceScopes.IdentitySessionValidate, XFrameworkServiceScopes.TenantTarget],
        AllowedServiceCallers = [XFrameworkServiceNames.Portal])]
    public static Task<Result<CheckVerificationResponse>> Handle(
        CheckVerificationRequest request,
        IAuthService authService,
        CancellationToken ct) => authService.CheckVerificationAsync(request, ct);

    [MapPost("/api/verifications/check", Tags = ["Verification"],
        Summary = "Check verification status",
        Description = "Checks if a valid (non-expired) verification exists for a credential. Verifications expire after 10 minutes.",
        RequireAuthorization = true,
        Capability = IdentityAuthorizationConstants.View,
        ExcludeFromOpenApi = false)]
    public static Task<Result<CheckVerificationResponse>> HandleHttp(
        CheckVerificationRequest request,
        HttpContext httpContext,
        IAuthService authService,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpDiagnostics(request.Metadata, httpContext);
        return authService.CheckVerificationAsync(request, ct);
    }
}

public class CheckVerificationRequestValidator : AbstractValidator<CheckVerificationRequest>
{
    public CheckVerificationRequestValidator()
    {
        RuleFor(x => x.CredentialId)
            .NotEmpty().WithMessage("Credential ID is required");

        RuleFor(x => x.VerificationTypeId)
            .NotEmpty().WithMessage("Verification Type ID is required");
    }
}
