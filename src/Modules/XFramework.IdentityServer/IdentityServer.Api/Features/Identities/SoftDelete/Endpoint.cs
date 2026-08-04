using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Identities.SoftDelete;

public static class SoftDeleteIdentityEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin],
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = ["identity.tenants:manage"])]
    public static Task<Result> Handle(
        SoftDeleteIdentityRequest request,
        IIdentityAdministrationService service,
        CancellationToken ct) =>
        service.SoftDeleteAsync(request, ct);

    [MapPost("/api/identities/delete", Tags = ["Identities"],
        Summary = "Delete an identity",
        Description = "Soft-deletes an identity and revokes its active sessions.",
        RequireAuthorization = true,
        Capability = IdentityAuthorizationConstants.Delete,
        Roles = ["SuperAdmin"],
        ExcludeFromOpenApi = false)]
    public static Task<Result> HandleHttp(
        SoftDeleteIdentityRequest request,
        HttpContext httpContext,
        IIdentityAdministrationService service,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpDiagnostics(request.Metadata, httpContext);
        return service.SoftDeleteAsync(request, ct);
    }
}

public sealed class SoftDeleteIdentityRequestValidator : AbstractValidator<SoftDeleteIdentityRequest>
{
    public SoftDeleteIdentityRequestValidator()
    {
        RuleFor(request => request.IdentityId).NotEmpty();
        RuleFor(request => request.ExpectedConcurrencyStamp).NotEmpty();
    }
}
