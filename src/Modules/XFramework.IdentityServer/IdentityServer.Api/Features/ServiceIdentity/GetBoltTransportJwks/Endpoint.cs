using XFramework.Integration.Attributes;
using XFramework.Integration.Security;

namespace IdentityServer.Api.Features.ServiceIdentity.GetBoltTransportJwks;

public static class GetBoltTransportJwksEndpoint
{
    [MapGet("/.well-known/bolt-transport-jwks.json", Tags = ["Service Identity"],
        Summary = "Get Bolt transport signing keys",
        Description = "Returns the public RSA key used to validate Bolt transport JWTs.",
        ExcludeFromOpenApi = true,
        ActorRequirement = ActorRequirement.None,
        TenantAccessMode = TenantAccessMode.Tenantless,
        AllowAnonymous = true)]
    public static Task<Result<BoltTransportJsonWebKeySet>> Handle(
        GetBoltTransportJwksRequest request,
        IBoltTransportTokenSigner signer,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(Result<BoltTransportJsonWebKeySet>.Success(signer.GetJsonWebKeySet()));
    }
}

public sealed class GetBoltTransportJwksRequest;
