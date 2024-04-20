using XFramework.Domain.Shared.Contracts.Requests;

namespace StreamFlow.Domain.Shared.Contracts.Requests;

public record StreamFlowSubscriptionRequest<TResponse> : RequestBase
{
    public StreamFlowSubscriptionRequest() { }
    public StreamFlowSubscriptionRequest(string name, Action<TResponse> onInvoke)
    {
        Name = name;
        OnInvoke = onInvoke;
    }
    public string Name { get; set; }
    public TResponse Response { get; set; }
    public Action<TResponse> OnInvoke { get; set; }
}

public class StreamFlowSubscriptionRequest
{
    public string Name { get; set; }
    public string Response { get; set; }
    public Action OnInvoke { get; set; }
}