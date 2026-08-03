using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Authorization.SetCredentialRolePermissionOverrides;

public static class SetCredentialRolePermissionOverridesEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin])]
    public static Task<Result<CredentialRolePermissionOverridesResponse>> Handle(
        SetCredentialRolePermissionOverridesRequest request,
        IIdentityAuthorizationService authorizationService,
        CancellationToken ct) =>
        authorizationService.SetCredentialRolePermissionOverridesAsync(request, ct);

    [MapPost("/api/identity/authorization/credential-role-overrides/set", Tags = ["Identity Authorization"],
        Summary = "Set credential role permission overrides",
        RequireAuthorization = true,
        Capability = IdentityAuthorizationConstants.Manage,
        ExcludeFromOpenApi = false)]
    public static Task<Result<CredentialRolePermissionOverridesResponse>> HandleHttp(
        SetCredentialRolePermissionOverridesRequest request,
        HttpContext httpContext,
        IIdentityAuthorizationService authorizationService,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpContextActor(request.Metadata, httpContext);
        return authorizationService.SetCredentialRolePermissionOverridesAsync(request, ct);
    }
}

public sealed class SetCredentialRolePermissionOverridesRequestValidator :
    AbstractValidator<SetCredentialRolePermissionOverridesRequest>
{
    public SetCredentialRolePermissionOverridesRequestValidator()
    {
        RuleFor(x => x.IdentityRoleId)
            .NotEmpty().WithMessage("Identity role is required");

        RuleFor(x => x.ExpectedConcurrencyStamp)
            .NotEmpty().WithMessage("Identity role version is required");

        RuleFor(x => x.Overrides)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(overrides => overrides.Count <= 500)
            .WithMessage("At most 500 credential role overrides can be updated at once");

        RuleForEach(x => x.Overrides)
            .NotNull()
            .SetValidator(new CapabilityPermissionDtoValidator())
            .When(x => x.Overrides is not null);
    }
}
