using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Integration.Attributes;
using IdentityServer.Api.Features.Tenants;

namespace IdentityServer.Api.Features.Tenants.Update;

public static class UpdateTenantEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin],
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = ["identity.tenants:manage"])]
    public static Task<Result<TenantAdministrationResponse>> Handle(
        UpdateTenantRequest request,
        ITenantAdministrationService service,
        CancellationToken ct) => service.UpdateAsync(request, ct);

    [MapPost("/api/tenants/update", Tags = ["Tenants"],
        Summary = "Update a tenant",
        Description = "Updates tenant details and lifecycle state through the IdentityServer admin workflow.",
        RequireAuthorization = true,
        Capability = IdentityAuthorizationConstants.Update,
        Roles = ["SuperAdmin"])]
    public static Task<Result<TenantAdministrationResponse>> HandleHttp(
        UpdateTenantRequest request,
        HttpContext httpContext,
        ITenantAdministrationService service,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpDiagnostics(request.Metadata, httpContext);
        return Handle(request, service, ct);
    }
}

public sealed class UpdateTenantRequestValidator : AbstractValidator<UpdateTenantRequest>
{
    public UpdateTenantRequestValidator()
    {
        RuleFor(request => request.TenantId).NotEmpty();
        RuleFor(request => request.Name).NotEmpty().MaximumLength(200);
        RuleFor(request => request.Version).GreaterThan(0);
        RuleFor(request => request.Status).Must(status => status is null or >= 0 and <= 3);
        RuleFor(request => request.ParentTenantId)
            .Must(parentTenantId => parentTenantId is null || parentTenantId != Guid.Empty);
        RuleFor(request => request.ConcurrencyStamp).NotEmpty();
    }
}
