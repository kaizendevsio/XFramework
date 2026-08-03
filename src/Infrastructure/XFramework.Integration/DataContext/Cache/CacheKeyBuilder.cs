using System.Security.Cryptography;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.DataContext;

namespace XFramework.Integration.DataContext.Cache;

internal static class CacheKeyBuilder
{
    public static string ForQuery<T>(QueryDescriptor descriptor, RequestMetadata metadata)
    {
        var hash = ComputeDescriptorHash(descriptor);
        var tenantId = metadata.TenantId
            ?? throw new InvalidOperationException("Tenant metadata is required for cached remote queries.");
        var credentialPartition = metadata.CredentialId?.ToString("N") ?? "none";
        return $"{typeof(T).Name}:tenant:{tenantId:N}:credential:{credentialPartition}:query:{hash}";
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
}
