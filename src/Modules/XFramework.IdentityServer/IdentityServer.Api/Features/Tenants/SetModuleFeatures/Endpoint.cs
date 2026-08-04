using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Tenants.SetModuleFeatures;

public static class SetTenantModuleFeaturesEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin],
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = ["identity.tenants:manage"])]
    public static Task<Result> Handle(
        SetTenantModuleFeaturesRequest request,
        IIdentityAuthorizationService service,
        CancellationToken ct) => service.SetTenantModuleFeaturesAsync(request, ct);

    [MapPost("/api/tenants/module-features", Tags = ["Tenants"],
        Summary = "Set tenant module features",
        Description = "Creates or updates tenant module feature configuration through the IdentityServer admin workflow.",
        RequireAuthorization = true,
        Capability = IdentityAuthorizationConstants.Manage,
        Roles = ["SuperAdmin"],
        ExcludeFromOpenApi = false)]
    public static Task<Result> HandleHttp(
        SetTenantModuleFeaturesRequest request,
        HttpContext httpContext,
        IIdentityAuthorizationService service,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpDiagnostics(request.Metadata, httpContext);
        return service.SetTenantModuleFeaturesAsync(request, ct);
    }
}

public sealed class SetTenantModuleFeaturesRequestValidator : AbstractValidator<SetTenantModuleFeaturesRequest>
{
    public SetTenantModuleFeaturesRequestValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.ExpectedConcurrencyStamp).NotEmpty();
        RuleFor(x => x.Features)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .NotEmpty()
            .Must(x => x.Count <= 200);
        RuleForEach(x => x.Features)
            .NotNull()
            .ChildRules(feature =>
            {
                feature.RuleFor(x => x.ModuleKey).NotEmpty().MaximumLength(100);
                feature.RuleFor(x => x.SubFeatureKey).MaximumLength(100);
                feature.RuleFor(x => x.DisplayName).MaximumLength(200);
                feature.RuleFor(x => x.Description).MaximumLength(1000);
            })
            .When(x => x.Features is not null);
    }
}
