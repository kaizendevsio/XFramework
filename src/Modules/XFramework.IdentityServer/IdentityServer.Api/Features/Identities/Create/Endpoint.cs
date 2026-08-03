using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.Identities.Create;

public static class CreateIdentityEndpoint
{
    [BoltHandler(RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin])]
    public static Task<Result<IdentityAdministrationResponse>> Handle(
        CreateIdentityRequest request,
        IIdentityAdministrationService service,
        CancellationToken ct) =>
        service.CreateAsync(request, ct);

    [MapPost("/api/identities", Tags = ["Identities"],
        Summary = "Create an identity",
        Description = "Creates a tenant-bound identity through the administration workflow.",
        RequireAuthorization = true,
        Capability = IdentityAuthorizationConstants.Create,
        Roles = ["SuperAdmin"])]
    public static Task<Result<IdentityAdministrationResponse>> HandleHttp(
        CreateIdentityRequest request,
        HttpContext httpContext,
        IIdentityAdministrationService service,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpContextActor(request.Metadata, httpContext);
        return service.CreateAsync(request, ct);
    }
}

public sealed class CreateIdentityRequestValidator : AbstractValidator<CreateIdentityRequest>
{
    public CreateIdentityRequestValidator()
    {
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
