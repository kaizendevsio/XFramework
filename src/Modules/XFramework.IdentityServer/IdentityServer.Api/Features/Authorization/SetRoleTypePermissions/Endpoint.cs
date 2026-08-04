using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Authorization.SetRoleTypePermissions;

public static class SetRoleTypePermissionsEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin],
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = ["identity.tenants:manage"])]
    public static Task<Result<RoleTypePermissionsResponse>> Handle(
        SetRoleTypePermissionsRequest request,
        IIdentityAuthorizationService authorizationService,
        CancellationToken ct) =>
        authorizationService.SetRoleTypePermissionsAsync(request, ct);

    [MapPost("/api/identity/authorization/role-type-permissions/set", Tags = ["Identity Authorization"],
        Summary = "Set role type permissions",
        RequireAuthorization = true,
        Capability = IdentityAuthorizationConstants.Manage,
        ExcludeFromOpenApi = false)]
    public static Task<Result<RoleTypePermissionsResponse>> HandleHttp(
        SetRoleTypePermissionsRequest request,
        HttpContext httpContext,
        IIdentityAuthorizationService authorizationService,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpDiagnostics(request.Metadata, httpContext);
        return authorizationService.SetRoleTypePermissionsAsync(request, ct);
    }
}

public sealed class SetRoleTypePermissionsRequestValidator : AbstractValidator<SetRoleTypePermissionsRequest>
{
    public SetRoleTypePermissionsRequestValidator()
    {
        RuleFor(x => x.RoleTypeId)
            .NotEmpty().WithMessage("Role type is required");

        RuleFor(x => x.ExpectedConcurrencyStamp)
            .NotEmpty().WithMessage("Role type version is required");

        RuleFor(x => x.Permissions)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(permissions => permissions.Count <= 500)
            .WithMessage("At most 500 role permissions can be updated at once");

        RuleForEach(x => x.Permissions)
            .NotNull()
            .SetValidator(new CapabilityPermissionDtoValidator())
            .When(x => x.Permissions is not null);
    }
}
