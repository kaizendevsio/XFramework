using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Authorization.RemoveCredentialRole;

public static class RemoveCredentialRoleEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin])]
    public static Task<Result> Handle(
        RemoveCredentialRoleRequest request,
        IIdentityAuthorizationService authorizationService,
        CancellationToken ct) =>
        authorizationService.RemoveCredentialRoleAsync(request, ct);

    [MapPost("/api/identity/authorization/roles/remove", Tags = ["Identity Authorization"],
        Summary = "Remove a credential role",
        RequireAuthorization = true,
        Capability = IdentityAuthorizationConstants.Manage,
        ExcludeFromOpenApi = false)]
    public static Task<Result> HandleHttp(
        RemoveCredentialRoleRequest request,
        HttpContext httpContext,
        IIdentityAuthorizationService authorizationService,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpContextActor(request.Metadata, httpContext);
        return authorizationService.RemoveCredentialRoleAsync(request, ct);
    }
}

public sealed class RemoveCredentialRoleRequestValidator : AbstractValidator<RemoveCredentialRoleRequest>
{
    public RemoveCredentialRoleRequestValidator()
    {
        RuleFor(x => x.IdentityRoleId)
            .NotEmpty().WithMessage("Identity role is required");
    }
}
