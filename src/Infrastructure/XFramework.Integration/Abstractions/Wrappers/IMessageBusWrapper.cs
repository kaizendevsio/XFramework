using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.Contracts.Base;

namespace XFramework.Integration.Abstractions.Wrappers;

public interface IMessageBusWrapper : IXFrameworkService
{
    public bool IsConnected { get; }
    public Action OnReconnected { get; set; }
    public Action OnReconnecting { get; set; }
    public Action OnDisconnected { get; set; }
    public Task<bool> Connect();
    public Task StartClientEventListener(string topic);

    public Task<CmdResponse> SendVoidAsync<TRequest>(TRequest request, string recipient, CancellationToken ct = default)
        where TRequest : class, IHasRequestServer;
    public Task<CmdResponse<TResponse>> SendVoidAsync<TRequest, TResponse>(TRequest request, string recipient, CancellationToken ct = default)
        where TRequest : class, IHasRequestServer;
    public Task<QueryResponse<TResponse>> SendAsync<TRequest, TResponse>(TRequest request, string recipient, CancellationToken ct = default)
        where TRequest : class, IHasRequestServer;
    public Task PublishAsync<TModel>(string eventName, string topic, TModel? data)
        where TModel : class, IHasRequestServer;
    public Task PublishAsync<TModel>(string eventName, string topic, TModel? data, bool durable)
        where TModel : class;
    public Task PublishAsync(string eventName, string topic);
    public Task Subscribe<TResponse>(BoltSubscriptionRequest<TResponse> request)
        where TResponse : class;
    public Task SubscribeAsync<TResponse>(string topic, Func<TResponse, Task> handler, CancellationToken ct = default)
        where TResponse : class;
    public Task SubscribeAsync<TResponse>(
        string topic,
        Func<TResponse, Task> handler,
        string? actorAccessToken,
        CancellationToken ct = default)
        where TResponse : class;
    public Task SubscribeAsync<TResponse>(
        string topic,
        Func<TResponse, Task> handler,
        Func<CancellationToken, ValueTask<string?>> actorAccessTokenProvider,
        CancellationToken ct = default)
        where TResponse : class;
    public Task Unsubscribe(BoltSubscriptionRequest request);
    public Task SubscribeDurableAsync<TResponse>(string topic, string subscriberId, Func<TResponse, Task> handler, CancellationToken ct = default)
        where TResponse : class;
    public Task SubscribeDurableAsync<TResponse>(
        string topic,
        string subscriberId,
        Func<TResponse, Task> handler,
        string? actorAccessToken,
        CancellationToken ct = default)
        where TResponse : class;
    public Task SubscribeDurableAsync<TResponse>(
        string topic,
        string subscriberId,
        Func<TResponse, Task> handler,
        Func<CancellationToken, ValueTask<string?>> actorAccessTokenProvider,
        CancellationToken ct = default)
        where TResponse : class;
}
