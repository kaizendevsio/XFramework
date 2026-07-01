using XFramework.Domain.Shared.ServiceIdentity;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.ServiceIdentity.RetireSigningKey;

public static class RetireServiceSigningKeyEndpoint
{
    [BoltHandler]
    [MapPost("/api/service-identity/signing-keys/retire", Tags = ["Service Identity"],
        Summary = "Retire service signing key",
        Description = "Retires a non-active service-token signing key.",
        ExcludeFromOpenApi = true)]
    public static async Task<Result<ServiceSigningKeyResponse>> Handle(
        RetireServiceSigningKeyRequest request,
        IServiceIdentityService serviceIdentityService,
        CancellationToken ct)
    {
        return await serviceIdentityService.RetireSigningKeyAsync(request, ct);
    }
}
