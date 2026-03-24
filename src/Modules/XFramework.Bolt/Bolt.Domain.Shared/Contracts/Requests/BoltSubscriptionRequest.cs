using XFramework.Domain.Shared.Contracts.Requests;

namespace Bolt.Domain.Shared.Contracts.Requests;

public record BoltSubscriptionRequest<TResponse> : RequestBase
{
    public BoltSubscriptionRequest() { }
    public BoltSubscriptionRequest(string name, Action<TResponse> onInvoke)
    {
        Name = name;
        OnInvoke = onInvoke;
    }
    public string Name { get; set; }
    public TResponse Response { get; set; }
    public Action<TResponse> OnInvoke { get; set; }
}

public class BoltSubscriptionRequest
{
    public string Name { get; set; }
    public string Response { get; set; }
    public Action OnInvoke { get; set; }
}