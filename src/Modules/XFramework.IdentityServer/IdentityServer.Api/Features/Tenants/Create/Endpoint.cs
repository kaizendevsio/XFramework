using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Integration.Attributes;
using IdentityServer.Api.Features.Tenants;

namespace IdentityServer.Api.Features.Tenants.Create;

public static class CreateTenantEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin],
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = ["identity.tenants:manage"])]
    public static Task<Result<TenantAdministrationResponse>> Handle(
        CreateTenantRequest request,
        ITenantAdministrationService service,
        CancellationToken ct) => service.CreateAsync(request, ct);

    [MapPost("/api/tenants", Tags = ["Tenants"],
        Summary = "Create a tenant",
        Description = "Creates a tenant through the IdentityServer admin workflow.",
        RequireAuthorization = true,
        Capability = IdentityAuthorizationConstants.Create,
        Roles = ["SuperAdmin"])]
    public static Task<Result<TenantAdministrationResponse>> HandleHttp(
        CreateTenantRequest request,
        HttpContext httpContext,
        ITenantAdministrationService service,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpDiagnostics(request.Metadata, httpContext);
        return Handle(
            request,
            service,
            ct);
    }
}

public class CreateTenantRequestValidator : AbstractValidator<CreateTenantRequest>
{
    public CreateTenantRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tenant name is required");

        RuleFor(x => x.Version)
            .GreaterThan(0).WithMessage("Version must be greater than zero");

        RuleFor(x => x.Status)
            .Must(status => status is null or >= 0 and <= 3)
            .WithMessage("Tenant status is invalid");

        RuleFor(x => x.ParentTenantId)
            .Must(parentTenantId => parentTenantId is null || parentTenantId != Guid.Empty)
            .WithMessage("Parent tenant is invalid");
    }
}
