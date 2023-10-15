using Microsoft.AspNetCore.SignalR.Client;
using StreamFlow.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.Configurations;

namespace XFramework.Integration.Abstractions;

public interface ISignalRService : IXFrameworkService
{
    HubConnection Connection { get; set; }
    StreamFlowConfiguration StreamFlowConfiguration { get; set; }

    Task<bool> EnsureConnection();
    Task StartEventListener(string topic);
    Task<HttpStatusCode> InvokeVoidAsync(string methodName, StreamFlowMessage sfMessage);
    Task<StreamFlowMessage> InvokeAsync(StreamFlowMessage sfMessage);
}