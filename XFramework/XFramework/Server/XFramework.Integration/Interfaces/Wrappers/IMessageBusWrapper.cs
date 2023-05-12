using Microsoft.AspNetCore.SignalR.Client;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.Interfaces;
using XFramework.Integration.Entity.Contracts.Responses;

namespace XFramework.Integration.Interfaces.Wrappers;

public interface IMessageBusWrapper : IXFrameworkService
{
    public HubConnectionState ConnectionState { get; }
    public Action OnReconnected { get; set; }
    public Action OnReconnecting { get; set; }
    public Action OnDisconnected { get; set; }
    public Task<bool> Connect();
    public Task StartClientEventListener(string topic);

    public Task<CmdResponse> SendVoidAsync<TRequest>(TRequest request, Guid? recipient) where TRequest : new();
    public Task<CmdResponse<TRequest>> SendAsync<TRequest>(TRequest request, Guid? recipient) where TRequest : new();
    public Task<QueryResponse<TResponse>> SendAsync<TRequest, TResponse>(TRequest request, Guid? recipient) where TRequest : new();
    public Task<StreamFlowInvokeResult<TResponse>> InvokeAsync<TResponse>(StreamFlowMessageBO request);

    public Task PublishAsync<TData>(string eventName, string topic, TData data);
    public Task PushAsync(StreamFlowMessageBO request);
    public Task Subscribe<TResponse>(StreamFlowSubscriptionRequest<TResponse> request);
    public Task Unsubscribe(StreamFlowSubscriptionRequest request);
}