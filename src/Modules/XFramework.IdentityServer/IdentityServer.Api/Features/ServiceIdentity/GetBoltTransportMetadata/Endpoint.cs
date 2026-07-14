using System.Text.Json.Serialization;
using XFramework.Integration.Attributes;

namespace IdentityServer.Api.Features.ServiceIdentity.GetBoltTransportMetadata;

public static class GetBoltTransportMetadataEndpoint
{
    [MapGet("/.well-known/openid-configuration", Tags = ["Service Identity"],
        Summary = "Get Bolt transport token metadata",
        Description = "Returns public issuer and signing-key discovery metadata for Bolt transport JWT validation.",
        ExcludeFromOpenApi = true)]
    public static Task<Result<BoltTransportMetadataResponse>> Handle(
        GetBoltTransportMetadataRequest request,
        ServiceIdentityConfiguration configuration,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        if (configuration.BoltTransportDiscoveryAuthority is not { } authority)
        {
            return Task.FromResult(Result<BoltTransportMetadataResponse>.Failure(
                "Bolt transport token discovery is unavailable",
                503));
        }

        return Task.FromResult(Result<BoltTransportMetadataResponse>.Success(new BoltTransportMetadataResponse
        {
            Issuer = configuration.Issuer,
            JsonWebKeySetUri = new Uri(authority, BoltTransportTokenConstants.JsonWebKeySetPath).AbsoluteUri,
            TokenEndpoint = new Uri(authority, BoltTransportTokenConstants.TokenEndpointPath).AbsoluteUri,
            SigningAlgorithms = [BoltTransportTokenConstants.Algorithm]
        }));
    }
}

public sealed class GetBoltTransportMetadataRequest;

public sealed record BoltTransportMetadataResponse
{
    [JsonPropertyName("issuer")]
    public required string Issuer { get; init; }

    [JsonPropertyName("jwks_uri")]
    public required string JsonWebKeySetUri { get; init; }

    [JsonPropertyName("token_endpoint")]
    public required string TokenEndpoint { get; init; }

    [JsonPropertyName("id_token_signing_alg_values_supported")]
    public required IReadOnlyList<string> SigningAlgorithms { get; init; }
}
