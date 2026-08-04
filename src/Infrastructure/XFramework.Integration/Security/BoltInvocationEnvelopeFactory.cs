using MemoryPack;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.ServiceIdentity;

namespace XFramework.Integration.Security;

public static class BoltInvocationEnvelopeFactory
{
    public static async Task<byte[]> CreateAsync<T>(
        T payload,
        string targetClient,
        IReadOnlyCollection<string>? scopes,
        IServiceTokenProvider serviceTokenProvider,
        IActorAccessTokenProvider actorAccessTokenProvider,
        CancellationToken ct = default)
    {
        var audience = ResolveCanonicalAudience(targetClient);
        var envelope = new BoltInvocationEnvelope
        {
            Payload = MemoryPackSerializer.Serialize(payload),
            ActorAccessToken = await actorAccessTokenProvider.GetTokenAsync(ct),
            ServiceAccessToken = await serviceTokenProvider.GetTokenAsync(audience, scopes, ct)
        };

        return MemoryPackSerializer.Serialize(envelope);
    }

    public static string ResolveCanonicalAudience(string targetClient)
    {
        var trimmed = targetClient.Trim();
        return XFrameworkServiceNames.All.FirstOrDefault(name =>
            string.Equals(name, trimmed, StringComparison.Ordinal) ||
            string.Equals(name.ToSha256(), trimmed, StringComparison.OrdinalIgnoreCase))
            ?? trimmed;
    }
}
