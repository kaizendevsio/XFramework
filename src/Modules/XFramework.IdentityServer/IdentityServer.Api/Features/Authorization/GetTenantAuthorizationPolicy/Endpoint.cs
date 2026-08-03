using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Authorization.GetTenantAuthorizationPolicy;

public static class GetTenantAuthorizationPolicyEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin])]
    public static Task<Result<TenantAuthorizationPolicyResponse>> Handle(
        GetTenantAuthorizationPolicyRequest request,
        IIdentityAuthorizationService authorizationService,
        CancellationToken ct) =>
        authorizationService.GetTenantAuthorizationPolicyAsync(request, ct);

    [MapPost("/api/identity/authorization/tenant-policy/get", Tags = ["Identity Authorization"],
        Summary = "Get tenant authorization policy",
        RequireAuthorization = true,
        Capability = IdentityAuthorizationConstants.View,
        ExcludeFromOpenApi = false)]
    public static Task<Result<TenantAuthorizationPolicyResponse>> HandleHttp(
        GetTenantAuthorizationPolicyRequest request,
        HttpContext httpContext,
        IIdentityAuthorizationService authorizationService,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpContextActor(request.Metadata, httpContext);
        return authorizationService.GetTenantAuthorizationPolicyAsync(request, ct);
    }
}

public sealed class GetTenantAuthorizationPolicyRequestValidator :
    AbstractValidator<GetTenantAuthorizationPolicyRequest>
{
    public GetTenantAuthorizationPolicyRequestValidator()
    {
        RuleFor(x => x.TenantId)
            .NotEmpty().WithMessage("Tenant is required");
    }
}
