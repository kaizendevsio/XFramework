using FluentValidation;
using IdentityServer.Api.Features.Authorization.Shared;
using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace IdentityServer.Api.Features.ServiceIdentity.RetireSigningKey;

public static class RetireServiceSigningKeyEndpoint
{
    [BoltHandler(
        ActorRequirement = ActorRequirement.Optional,
        TenantAccessMode = TenantAccessMode.Tenantless,
        RequiredServiceScopes = [XFrameworkServiceScopes.IdentityAdmin])]
    public static Task<Result<ServiceSigningKeyResponse>> Handle(
        RetireServiceSigningKeyRequest request,
        IServiceIdentityService serviceIdentityService,
        CancellationToken ct) => serviceIdentityService.RetireSigningKeyAsync(request, ct);

    [MapPost("/api/service-identity/signing-keys/retire", Tags = ["Service Identity"],
        Summary = "Retire service signing key",
        Description = "Retires a non-active service-token signing key.",
        RequireAuthorization = true,
        Roles = ["SuperAdmin"],
        ExcludeFromOpenApi = false)]
    public static Task<Result<ServiceSigningKeyResponse>> HandleHttp(
        RetireServiceSigningKeyRequest request,
        HttpContext httpContext,
        IServiceIdentityService serviceIdentityService,
        CancellationToken ct)
    {
        IdentityAuthorizationEndpointMetadata.ApplyHttpDiagnostics(request.Metadata, httpContext);
        return serviceIdentityService.RetireSigningKeyAsync(request, ct);
    }
}

public sealed class RetireServiceSigningKeyRequestValidator : AbstractValidator<RetireServiceSigningKeyRequest>
{
    public RetireServiceSigningKeyRequestValidator() =>
        RuleFor(request => request.KeyId).NotEmpty().MaximumLength(128);
}
