using MemoryPack;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.ServiceIdentity;

namespace XFramework.Integration.Security;

public static class BoltInvocationEnvelopeFactory
{
    public static async Task<byte[]> CreateDataContextQueryAsync(
        QueryDescriptor descriptor,
        string targetClient,
        IServiceTokenProvider serviceTokenProvider,
        IActorAccessTokenProvider actorAccessTokenProvider,
        CancellationToken ct = default)
    {
        // Resolve the actor once so scope selection and the envelope use the same identity.
        // The receiving service still authorizes the actor and derives the effective tenant.
        var actorToken = await actorAccessTokenProvider.GetTokenAsync(ct);
        var scopes = new List<string> { XFrameworkServiceScopes.DataContextQuery };
        if (descriptor.IgnoreQueryFilters)
            scopes.Add(XFrameworkServiceScopes.DataContextQueryAllTenants);
        if (descriptor.IgnoreQueryFilters || string.IsNullOrWhiteSpace(actorToken))
            scopes.Add(XFrameworkServiceScopes.TenantTarget);

        return MemoryPackSerializer.Serialize(new BoltInvocationEnvelope
        {
            Payload = MemoryPackSerializer.Serialize(descriptor),
            ActorAccessToken = actorToken,
            ServiceAccessToken = await serviceTokenProvider.GetTokenAsync(
                ResolveCanonicalAudience(targetClient), scopes, ct)
        });
    }

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
