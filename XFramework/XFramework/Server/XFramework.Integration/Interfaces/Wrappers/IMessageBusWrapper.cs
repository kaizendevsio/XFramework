using Microsoft.AspNetCore.SignalR.Client;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.Interfaces;
using XFramework.Integration.Entity.Contracts.Responses;

namespace XFramework.Integration.Interfaces.Wrappers;

public interface IMessageBusWrapper : IXFrameworkService
{
    public Guid? TargetClient { get; set; }
    public HubConnectionState ConnectionState { get; }
    public Action OnReconnected { get; set; }
    public Action OnReconnecting { get; set; }
    public Action OnDisconnected { get; set; }
    public Task<bool> Connect();
    public Task<StreamFlowInvokeResult<TResponse>> InvokeAsync<TResponse>(StreamFlowMessageBO request);
    public Task PushAsync(StreamFlowMessageBO request);
    /*public Task Subscribe(StreamFlowClientBO request);
    public Task Unsubscribe(StreamFlowClientBO request);*/
}