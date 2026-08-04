using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Identities.UpdateProfile;

public static class UpdateIdentityProfileEndpoint
{
    [BoltHandler(
        RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin],
        TenantAccessMode = TenantAccessMode.DelegatedTenant,
        RequiredActorCapabilities = ["identity.tenants:manage"])]
    public static Task<Result<IdentityAdministrationResponse>> Handle(
        UpdateIdentityProfileRequest request,
        IIdentityAdministrationService service,
        CancellationToken ct) =>
        service.UpdateProfileAsync(request, ct);

    [MapPost("/api/identities/profile", Tags = ["Identities"],
        Summary = "Update an identity profile",
        Description = "Updates non-verification identity profile fields.",
        RequireAuthorization = true,
        Capability = IdentityAuthorizationConstants.Update,
        Roles = ["SuperAdmin"])]
    public static Task<Result<IdentityAdministrationResponse>> HandleHttp(
        UpdateIdentityProfileRequest request,
        HttpContext httpContext,
        IIdentityAdministrationService service,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpDiagnostics(request.Metadata, httpContext);
        return service.UpdateProfileAsync(request, ct);
    }
}

public sealed class UpdateIdentityProfileRequestValidator : AbstractValidator<UpdateIdentityProfileRequest>
{
    public UpdateIdentityProfileRequestValidator()
    {
        RuleFor(request => request.IdentityId).NotEmpty();
        RuleFor(request => request.ExpectedConcurrencyStamp).NotEmpty();
        RuleFor(request => request)
            .Must(request =>
                !string.IsNullOrWhiteSpace(request.IdentityName)
                || !string.IsNullOrWhiteSpace(request.FirstName)
                || !string.IsNullOrWhiteSpace(request.LastName))
            .WithMessage("An identity name or person name is required");
        RuleFor(request => request.FirstName).MaximumLength(100);
        RuleFor(request => request.MiddleName).MaximumLength(100);
        RuleFor(request => request.LastName).MaximumLength(100);
        RuleFor(request => request.Suffix).MaximumLength(50);
        RuleFor(request => request.IdentityName).MaximumLength(100);
        RuleFor(request => request.IdentityDescription).MaximumLength(100);
    }
}
