using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Authorization.GetEffectiveCredentialCapabilities;

public static class GetEffectiveCredentialCapabilitiesEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin],
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = ["identity.tenants:manage"])]
    public static Task<Result<EffectiveCredentialCapabilitiesResponse>> Handle(
        GetEffectiveCredentialCapabilitiesRequest request,
        IIdentityAuthorizationService authorizationService,
        CancellationToken ct) =>
        authorizationService.GetEffectiveCredentialCapabilitiesAsync(request, ct);

    [MapPost("/api/identity/authorization/effective-capabilities", Tags = ["Identity Authorization"],
        Summary = "Get effective credential capabilities",
        RequireAuthorization = true,
        Capability = IdentityAuthorizationConstants.View,
        ExcludeFromOpenApi = false)]
    public static Task<Result<EffectiveCredentialCapabilitiesResponse>> HandleHttp(
        GetEffectiveCredentialCapabilitiesRequest request,
        HttpContext httpContext,
        IIdentityAuthorizationService authorizationService,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpDiagnostics(request.Metadata, httpContext);
        return authorizationService.GetEffectiveCredentialCapabilitiesAsync(request, ct);
    }
}

public sealed class GetEffectiveCredentialCapabilitiesRequestValidator :
    AbstractValidator<GetEffectiveCredentialCapabilitiesRequest>
{
    public GetEffectiveCredentialCapabilitiesRequestValidator()
    {
        RuleFor(x => x.CredentialId)
            .NotEmpty().WithMessage("Credential is required");
    }
}
