using System.Text.Json;
using MediatR;

namespace XFramework.Integration.Services.Helpers;

public static class StreamFlowHelper
{
    public static string AsStreamRequest(this object entity)
    {
        return entity.GetType().Name.Replace("Request", string.Empty);
    }
        
    public static TQuery AsMediatorCmd<TRequest, TQuery, TResponse>(this string entity) where TRequest : new() where TQuery : IRequest<TResponse>
    {
        return JsonSerializer.Deserialize<TRequest>(entity).Adapt<TQuery>();
    }

}