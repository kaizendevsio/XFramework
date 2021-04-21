using System;
using System.Text.Json;
using Mapster;

namespace XFramework.Integration.Services.Helpers
{
    public static class StreamFlowHelper
    {
        public static string AsStreamRequest(this object entity)
        {
            return entity.GetType().Name.Replace("Request", string.Empty);
        }
        
        public static TCmd AsMediatorCmd<TRequest, TCmd>(this string entity)
        {
            return JsonSerializer.Deserialize<TRequest>(entity).Adapt<TCmd>();
        }

    }
}