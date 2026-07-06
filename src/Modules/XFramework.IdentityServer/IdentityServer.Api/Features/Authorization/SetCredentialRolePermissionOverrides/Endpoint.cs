using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Authorization.SetCredentialRolePermissionOverrides;

public static class SetCredentialRolePermissionOverridesEndpoint
{
    [BoltHandler]
    public static Task<Result<CredentialRolePermissionOverridesResponse>> Handle(
        SetCredentialRolePermissionOverridesRequest request,
        IIdentityAuthorizationService authorizationService,
        CancellationToken ct) =>
        authorizationService.SetCredentialRolePermissionOverridesAsync(request, ct);

    [MapPost("/api/identity/authorization/credential-role-overrides/set", Tags = ["Identity Authorization"],
        Summary = "Set credential role permission overrides",
        RequireAuthorization = true,
        ExcludeFromOpenApi = true)]
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

        RuleForEach(x => x.Overrides)
            .SetValidator(new CapabilityPermissionDtoValidator());
    }
}
