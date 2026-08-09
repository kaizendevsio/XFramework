using System.Security.Cryptography;
using System.Text;
using XFramework.Domain.Shared.DataContext;
using XFramework.Domain.Shared.Security;
using XFramework.Integration.Security;

namespace XFramework.Integration.DataContext.Cache;

internal static class CacheKeyBuilder
{
    public static string ForQuery<T>(QueryDescriptor descriptor, TrustedInvocationContext context)
    {
        var hash = ComputeDescriptorHash(descriptor);
        var tenantSegment = context.EffectiveTenantId is { } tenantId
            ? tenantId.ToString("N")
            : "tenantless";
        var authorityHash = ComputeAuthorityHash(context);
        return $"{typeof(T).Name}:tenant:{tenantSegment}:authority:{authorityHash}:query:{hash}";
    }

    public static string PrefixForEntity<T>()
        => $"{typeof(T).Name}:";

    public static string PrefixForEntity(string entityTypeName)
        => $"{entityTypeName}:";

    private static string ComputeDescriptorHash(QueryDescriptor descriptor)
    {
        var bytes = MemoryPack.MemoryPackSerializer.Serialize(descriptor);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash)[..16];
    }

    private static string ComputeAuthorityHash(TrustedInvocationContext context)
    {
        var actor = context.Actor;
        var service = context.Service;
        var authority = string.Join('|',
            actor?.CredentialId.ToString("N") ?? string.Empty,
            actor?.SessionId.ToString("N") ?? string.Empty,
            actor?.GenerationId ?? string.Empty,
            JoinSorted(actor?.Roles),
            JoinSorted(actor?.Capabilities),
            JoinSorted(actor?.Attributes),
            service?.ClientId ?? string.Empty,
            service?.GenerationId ?? string.Empty,
            JoinSorted(service?.Scopes),
            GeneratedAuthorizationPolicyVersion.Current.ToString());
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(authority));
        return Convert.ToHexStringLower(hash)[..16];
    }

    private static string JoinSorted(IReadOnlySet<string>? values) => values is null
        ? string.Empty
        : string.Join(',', values.Order(StringComparer.OrdinalIgnoreCase));

    private static string JoinSorted(IReadOnlyDictionary<string, string>? values) => values is null
        ? string.Empty
        : string.Join(',', values
            .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static pair => $"{pair.Key}={pair.Value}"));
}
