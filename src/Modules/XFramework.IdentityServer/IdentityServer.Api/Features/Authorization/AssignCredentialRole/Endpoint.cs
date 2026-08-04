using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Authorization.AssignCredentialRole;

public static class AssignCredentialRoleEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin],
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = ["identity.tenants:manage"])]
    public static Task<Result<AssignedCredentialRoleResponse>> Handle(
        AssignCredentialRoleRequest request,
        IIdentityAuthorizationService authorizationService,
        CancellationToken ct) =>
        authorizationService.AssignCredentialRoleAsync(request, ct);

    [MapPost("/api/identity/authorization/roles/assign", Tags = ["Identity Authorization"],
        Summary = "Assign a credential role",
        RequireAuthorization = true,
        Capability = IdentityAuthorizationConstants.Manage)]
    public static Task<Result<AssignedCredentialRoleResponse>> HandleHttp(
        AssignCredentialRoleRequest request,
        HttpContext httpContext,
        IIdentityAuthorizationService authorizationService,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpDiagnostics(request.Metadata, httpContext);
        return authorizationService.AssignCredentialRoleAsync(request, ct);
    }
}

public sealed class AssignCredentialRoleRequestValidator : AbstractValidator<AssignCredentialRoleRequest>
{
    public AssignCredentialRoleRequestValidator()
    {
        RuleFor(x => x.CredentialId)
            .NotEmpty().WithMessage("Credential is required");

        RuleFor(x => x.RoleTypeId)
            .NotEmpty().WithMessage("Role type is required");
    }
}
