using System.Security.Cryptography;
using XFramework.Domain.Shared.DataContext;

namespace XFramework.Integration.DataContext.Cache;

internal static class CacheKeyBuilder
{
    public static string ForQuery<T>(QueryDescriptor descriptor)
    {
        var hash = ComputeDescriptorHash(descriptor);
        return $"{typeof(T).Name}:query:{hash}";
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
