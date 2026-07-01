using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.ServiceIdentity.RotateSigningKey;

public static class RotateServiceSigningKeyEndpoint
{
    [BoltHandler]
    [MapPost("/api/service-identity/signing-keys/rotate", Tags = ["Service Identity"],
        Summary = "Rotate service signing key",
        Description = "Creates and activates a new service-token signing key.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<ServiceSigningKeyResponse>> Handle(
        RotateServiceSigningKeyRequest request,
        IServiceIdentityService serviceIdentityService,
        CancellationToken ct)
    {
        return await serviceIdentityService.RotateSigningKeyAsync(request, ct);
    }
}
