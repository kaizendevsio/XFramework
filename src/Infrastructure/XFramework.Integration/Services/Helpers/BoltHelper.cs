using MemoryPack;

namespace XFramework.Integration.Services.Helpers;

public static class BoltHelper
{
    public static string AsStreamRequest(this object entity)
    {
        return entity.GetType().Name.Replace("Request", string.Empty);
    }
        
    public static TRequest AsCommandQuery<TRequest>(this ReadOnlyMemory<byte> data)
        where TRequest : class
    {
        return MemoryPackSerializer.Deserialize<TRequest>(data.Span);
    }

}