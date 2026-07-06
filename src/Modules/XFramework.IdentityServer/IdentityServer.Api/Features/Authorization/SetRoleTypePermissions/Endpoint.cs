using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Authorization.SetRoleTypePermissions;

public static class SetRoleTypePermissionsEndpoint
{
    [BoltHandler]
    public static Task<Result<RoleTypePermissionsResponse>> Handle(
        SetRoleTypePermissionsRequest request,
        IIdentityAuthorizationService authorizationService,
        CancellationToken ct) =>
        authorizationService.SetRoleTypePermissionsAsync(request, ct);

    [MapPost("/api/identity/authorization/role-type-permissions/set", Tags = ["Identity Authorization"],
        Summary = "Set role type permissions",
        RequireAuthorization = true,
        ExcludeFromOpenApi = true)]
    public static Task<Result<RoleTypePermissionsResponse>> HandleHttp(
        SetRoleTypePermissionsRequest request,
        HttpContext httpContext,
        IIdentityAuthorizationService authorizationService,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpContextActor(request.Metadata, httpContext);
        return authorizationService.SetRoleTypePermissionsAsync(request, ct);
    }
}

public sealed class SetRoleTypePermissionsRequestValidator : AbstractValidator<SetRoleTypePermissionsRequest>
{
    public SetRoleTypePermissionsRequestValidator()
    {
        RuleFor(x => x.RoleTypeId)
            .NotEmpty().WithMessage("Role type is required");

        RuleForEach(x => x.Permissions)
            .SetValidator(new CapabilityPermissionDtoValidator());
    }
}
