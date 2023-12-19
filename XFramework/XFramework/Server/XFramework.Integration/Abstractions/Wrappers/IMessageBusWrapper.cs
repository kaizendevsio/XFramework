using Microsoft.AspNetCore.SignalR.Client;
using StreamFlow.Domain.Generic.Abstractions;
using StreamFlow.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.Contracts.Base;
using XFramework.Integration.Entity.Contracts.Responses;

namespace XFramework.Integration.Abstractions.Wrappers;

public interface IMessageBusWrapper : IXFrameworkService
{
    public HubConnectionState ConnectionState { get; }
    public Action OnReconnected { get; set; }
    public Action OnReconnecting { get; set; }
    public Action OnDisconnected { get; set; }
    public Task<bool> Connect();
    public Task StartClientEventListener(string topic);

    public Task<CmdResponse> SendVoidAsync<TRequest>(TRequest request, Guid? recipient) 
        where TRequest : class, IHasRequestServer;
    public Task<CmdResponse<TRequest>> SendAsync<TRequest>(TRequest request, Guid? recipient) 
        where TRequest : class, IHasRequestServer;
    public Task<QueryResponse<TResponse>> SendAsync<TRequest, TResponse>(TRequest request, Guid? recipient) 
        where TRequest : class, IHasRequestServer;
    public Task<StreamFlowInvokeResult<TResponse>> InvokeAsync<TModel,TResponse>(StreamFlowMessage<TModel> request) 
        where TModel : class, IHasRequestServer
        where TResponse : class, IBaseResponse;
    public Task PublishAsync<TModel>(string eventName, string topic, TModel? data) 
        where TModel : class, IHasRequestServer;
    public Task PublishAsync(string eventName, string topic);
    public Task PushAsync<TModel>(StreamFlowMessage<TModel> request) 
        where TModel : class, IHasRequestServer;
    public Task Subscribe<TResponse>(StreamFlowSubscriptionRequest<TResponse> request) 
        where TResponse : class;
    public Task Unsubscribe(StreamFlowSubscriptionRequest request);
}