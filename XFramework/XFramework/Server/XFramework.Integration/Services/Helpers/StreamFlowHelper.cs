using MediatR;
using MessagePack;

namespace XFramework.Integration.Services.Helpers;

public static class StreamFlowHelper
{
    public static string AsStreamRequest(this object entity)
    {
        return entity.GetType().Name.Replace("Request", string.Empty);
    }
        
    public static TQuery AsMediatorCmd<TRequest, TQuery, TResponse>(this byte[] entity) 
        where TRequest : class, new() 
        where TQuery : IRequest<TResponse>
    {
        var deserializedEntity = MessagePackSerializer.Deserialize<TRequest>(entity);
        if(deserializedEntity == null)
        {
            throw new InvalidOperationException("Failed to deserialize entity.");
        }

        return deserializedEntity.Adapt<TQuery>();
    }

}