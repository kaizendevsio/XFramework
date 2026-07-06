using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Authorization.UpdateTenantAuthorizationPolicy;

public static class UpdateTenantAuthorizationPolicyEndpoint
{
    [BoltHandler]
    public static Task<Result<TenantAuthorizationPolicyResponse>> Handle(
        UpdateTenantAuthorizationPolicyRequest request,
        IIdentityAuthorizationService authorizationService,
        CancellationToken ct) =>
        authorizationService.UpdateTenantAuthorizationPolicyAsync(request, ct);

    [MapPost("/api/identity/authorization/tenant-policy/update", Tags = ["Identity Authorization"],
        Summary = "Update tenant authorization policy",
        RequireAuthorization = true,
        ExcludeFromOpenApi = true)]
    public static Task<Result<TenantAuthorizationPolicyResponse>> HandleHttp(
        UpdateTenantAuthorizationPolicyRequest request,
        HttpContext httpContext,
        IIdentityAuthorizationService authorizationService,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpContextActor(request.Metadata, httpContext);
        return authorizationService.UpdateTenantAuthorizationPolicyAsync(request, ct);
    }
}

public sealed class UpdateTenantAuthorizationPolicyRequestValidator :
    AbstractValidator<UpdateTenantAuthorizationPolicyRequest>
{
    public UpdateTenantAuthorizationPolicyRequestValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant is required");

        RuleFor(x => x.MissingPermissionBehavior)
            .IsInEnum().WithMessage("Missing permission behavior is invalid");
    }
}
