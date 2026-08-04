using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Authorization.GetCredentialRolePermissionOverrides;

public static class GetCredentialRolePermissionOverridesEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin],
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = ["identity.tenants:manage"])]
    public static Task<Result<CredentialRolePermissionOverridesResponse>> Handle(
        GetCredentialRolePermissionOverridesRequest request,
        IIdentityAuthorizationService authorizationService,
        CancellationToken ct) =>
        authorizationService.GetCredentialRolePermissionOverridesAsync(request, ct);

    [MapPost("/api/identity/authorization/credential-role-overrides/get", Tags = ["Identity Authorization"],
        Summary = "Get credential role permission overrides",
        RequireAuthorization = true,
        Capability = IdentityAuthorizationConstants.View,
        ExcludeFromOpenApi = false)]
    public static Task<Result<CredentialRolePermissionOverridesResponse>> HandleHttp(
        GetCredentialRolePermissionOverridesRequest request,
        HttpContext httpContext,
        IIdentityAuthorizationService authorizationService,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpDiagnostics(request.Metadata, httpContext);
        return authorizationService.GetCredentialRolePermissionOverridesAsync(request, ct);
    }
}

public sealed class GetCredentialRolePermissionOverridesRequestValidator :
    AbstractValidator<GetCredentialRolePermissionOverridesRequest>
{
    public GetCredentialRolePermissionOverridesRequestValidator()
    {
        RuleFor(x => x.IdentityRoleId)
            .NotEmpty().WithMessage("Identity role is required");
    }
}
