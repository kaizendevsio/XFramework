using System.Text.Json;
using BinaryPack;

namespace XFramework.Integration.Services.Helpers;

public static class StreamFlowHelper
{
    public static string AsStreamRequest(this object entity)
    {
        return entity.GetType().Name.Replace("Request", string.Empty);
    }
        
    public static TCmd AsMediatorCmd<TRequest, TCmd>(this byte[] entity) where TRequest : new()
    {
        return BinaryConverter.Deserialize<TRequest>(entity).Adapt<TCmd>();
    }

}