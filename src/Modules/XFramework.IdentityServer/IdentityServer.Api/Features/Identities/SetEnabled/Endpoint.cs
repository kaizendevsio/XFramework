using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Identities.SetEnabled;

public static class SetIdentityEnabledEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin],
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = ["identity.tenants:manage"])]
    public static Task<Result<IdentityAdministrationResponse>> Handle(
        SetIdentityEnabledRequest request,
        IIdentityAdministrationService service,
        CancellationToken ct) =>
        service.SetEnabledAsync(request, ct);

    [MapPost("/api/identities/enabled", Tags = ["Identities"],
        Summary = "Enable or disable an identity",
        Description = "Updates identity lifecycle state and revokes sessions when disabled.",
        RequireAuthorization = true,
        Capability = IdentityAuthorizationConstants.Manage,
        Roles = ["SuperAdmin"])]
    public static Task<Result<IdentityAdministrationResponse>> HandleHttp(
        SetIdentityEnabledRequest request,
        HttpContext httpContext,
        IIdentityAdministrationService service,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpDiagnostics(request.Metadata, httpContext);
        return service.SetEnabledAsync(request, ct);
    }
}

public sealed class SetIdentityEnabledRequestValidator : AbstractValidator<SetIdentityEnabledRequest>
{
    public SetIdentityEnabledRequestValidator()
    {
        RuleFor(request => request.IdentityId).NotEmpty();
        RuleFor(request => request.ExpectedConcurrencyStamp).NotEmpty();
    }
}
