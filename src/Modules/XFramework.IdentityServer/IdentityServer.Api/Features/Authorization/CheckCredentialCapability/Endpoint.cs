using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Authorization.CheckCredentialCapability;

public static class CheckCredentialCapabilityEndpoint
{
    [BoltHandler]
    public static Task<Result<CredentialCapabilityCheckResponse>> Handle(
        CheckCredentialCapabilityRequest request,
        IIdentityAuthorizationService authorizationService,
        CancellationToken ct) =>
        authorizationService.CheckCredentialCapabilityAsync(request, ct);

    [MapPost("/api/identity/authorization/check-capability", Tags = ["Identity Authorization"],
        Summary = "Check a credential capability",
        RequireAuthorization = true,
        ExcludeFromOpenApi = true)]
    public static Task<Result<CredentialCapabilityCheckResponse>> HandleHttp(
        CheckCredentialCapabilityRequest request,
        HttpContext httpContext,
        IIdentityAuthorizationService authorizationService,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpContextActor(request.Metadata, httpContext);
        return authorizationService.CheckCredentialCapabilityAsync(request, ct);
    }
}

public sealed class CheckCredentialCapabilityRequestValidator : AbstractValidator<CheckCredentialCapabilityRequest>
{
    public CheckCredentialCapabilityRequestValidator()
    {
        RuleFor(x => x.CredentialId)
            .NotEmpty().WithMessage("Credential is required");

        RuleFor(x => x.ModuleKey)
            .NotEmpty().WithMessage("Module key is required");

        RuleFor(x => x.CapabilityKey)
            .NotEmpty().WithMessage("Capability key is required")
            .Must(BeKnownCapability).WithMessage("Capability key is invalid");
    }

    private static bool BeKnownCapability(string? value) =>
        IdentityAuthorizationConstants.CapabilityKeys.Contains(value?.Trim().ToLowerInvariant());
}
