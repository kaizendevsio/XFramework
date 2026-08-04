using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace IdentityServer.Api.Features.ServiceIdentity.RotateSigningKey;

public static class RotateServiceSigningKeyEndpoint
{
    [BoltHandler(
        ActorRequirement = ActorRequirement.Optional,
        TenantAccessMode = TenantAccessMode.Tenantless,
        RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin])]
    public static Task<Result<ServiceSigningKeyResponse>> Handle(
        RotateServiceSigningKeyRequest request,
        IServiceIdentityService serviceIdentityService,
        CancellationToken ct) => serviceIdentityService.RotateSigningKeyAsync(request, ct);

    [MapPost("/api/service-identity/signing-keys/rotate", Tags = ["Service Identity"],
        Summary = "Rotate service signing key",
        Description = "Creates and activates a new service-token signing key.",
        RequireAuthorization = true,
        Roles = ["SuperAdmin"],
        ExcludeFromOpenApi = false)]
    public static Task<Result<ServiceSigningKeyResponse>> HandleHttp(
        RotateServiceSigningKeyRequest request,
        HttpContext httpContext,
        IServiceIdentityService serviceIdentityService,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpDiagnostics(request.Metadata, httpContext);
        return serviceIdentityService.RotateSigningKeyAsync(request, ct);
    }
}

public sealed class RotateServiceSigningKeyRequestValidator : AbstractValidator<RotateServiceSigningKeyRequest>
{
    public RotateServiceSigningKeyRequestValidator() =>
        RuleFor(request => request.Reason).MaximumLength(256);
}
