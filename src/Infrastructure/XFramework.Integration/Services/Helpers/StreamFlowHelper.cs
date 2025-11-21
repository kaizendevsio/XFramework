using MemoryPack;
using MessagePack;
using XFramework.Domain.Shared.Contracts.Requests;

namespace XFramework.Integration.Services.Helpers;

public static class StreamFlowHelper
{
    public static string AsStreamRequest(this object entity)
    {
        return entity.GetType().Name.Replace("Request", string.Empty);
    }
        
    public static TRequest AsCommandQuery<TRequest>(this object entity)
        where TRequest : class
    {
        var deserializedEntity = MemoryPackSerializer.Deserialize<TRequest>(entity as byte[]);
        return deserializedEntity;
    }

}