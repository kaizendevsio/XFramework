using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Authorization.GetRoleTypePermissions;

public static class GetRoleTypePermissionsEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin],
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = ["identity.tenants:manage"])]
    public static Task<Result<RoleTypePermissionsResponse>> Handle(
        GetRoleTypePermissionsRequest request,
        IIdentityAuthorizationService authorizationService,
        CancellationToken ct) =>
        authorizationService.GetRoleTypePermissionsAsync(request, ct);

    [MapPost("/api/identity/authorization/role-type-permissions/get", Tags = ["Identity Authorization"],
        Summary = "Get role type permissions",
        RequireAuthorization = true,
        Capability = IdentityAuthorizationConstants.View,
        ExcludeFromOpenApi = false)]
    public static Task<Result<RoleTypePermissionsResponse>> HandleHttp(
        GetRoleTypePermissionsRequest request,
        HttpContext httpContext,
        IIdentityAuthorizationService authorizationService,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpDiagnostics(request.Metadata, httpContext);
        return authorizationService.GetRoleTypePermissionsAsync(request, ct);
    }
}

public sealed class GetRoleTypePermissionsRequestValidator : AbstractValidator<GetRoleTypePermissionsRequest>
{
    public GetRoleTypePermissionsRequestValidator()
    {
        RuleFor(x => x.RoleTypeId)
            .NotEmpty().WithMessage("Role type is required");
    }
}
