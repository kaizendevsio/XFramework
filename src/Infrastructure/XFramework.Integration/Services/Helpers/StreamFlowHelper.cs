using MediatR;
using MemoryPack;
using MessagePack;

namespace XFramework.Integration.Services.Helpers;

public static class StreamFlowHelper
{
    public static string AsStreamRequest(this object entity)
    {
        return entity.GetType().Name.Replace("Request", string.Empty);
    }
        
    public static async Task<TQuery> AsMediatorCmd<TQuery, TResponse>(this object entity) 
        where TQuery : class, IRequest<TResponse>
    {
        var deserializedEntity = await MemoryPackSerializer.DeserializeAsync<TQuery>(new MemoryStream(entity as byte[])); 
        return deserializedEntity;
    }

}