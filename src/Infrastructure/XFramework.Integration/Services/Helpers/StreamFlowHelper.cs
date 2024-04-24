using MediatR;
using MessagePack;

namespace XFramework.Integration.Services.Helpers;

public static class StreamFlowHelper
{
    public static string AsStreamRequest(this object entity)
    {
        return entity.GetType().Name.Replace("Request", string.Empty);
    }
        
    public static TQuery AsMediatorCmd<TQuery, TResponse>(this object entity) 
        where TQuery : class, IRequest<TResponse>
    {
        var deserializedEntity =  MessagePackSerializer.Deserialize<TQuery>(entity as byte[], new(MessagePack.Resolvers.ContractlessStandardResolver.Instance)); 
        return deserializedEntity;
    }

}