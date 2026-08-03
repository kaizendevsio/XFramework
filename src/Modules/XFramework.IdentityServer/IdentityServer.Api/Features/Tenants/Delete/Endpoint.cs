using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Integration.Attributes;
using IdentityServer.Api.Features.Tenants;

namespace IdentityServer.Api.Features.Tenants.Delete;

public static class DeleteTenantEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin])]
    public static Task<Result> Handle(
        DeleteTenantRequest request,
        ITenantAdministrationService service,
        CancellationToken ct) => service.DeleteAsync(request, ct);

    [MapPost("/api/tenants/delete", Tags = ["Tenants"],
        Summary = "Delete a tenant",
        Description = "Soft-deletes a tenant through the IdentityServer admin workflow.",
        RequireAuthorization = true,
        Capability = IdentityAuthorizationConstants.Delete,
        Roles = ["SuperAdmin"],
        ExcludeFromOpenApi = false)]
    public static Task<Result> HandleHttp(
        DeleteTenantRequest request,
        HttpContext httpContext,
        ITenantAdministrationService service,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpContextActor(request.Metadata, httpContext);
        return Handle(
            request,
            service,
            ct);
    }
}

public class DeleteTenantRequestValidator : AbstractValidator<DeleteTenantRequest>
{
    public DeleteTenantRequestValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant ID is required");
        RuleFor(x => x.ExpectedConcurrencyStamp)
            .NotEmpty().WithMessage("Expected concurrency stamp is required");
    }
}
