using Microsoft.AspNetCore.SignalR.Client;
using Bolt.Domain.Shared.Contracts.Requests;
using Bolt.Domain.Shared.Contracts.Responses;
using XFramework.Domain.Shared.Configurations;

namespace XFramework.Integration.Abstractions;

public interface ISignalRService : IXFrameworkService
{
    HubConnection? Connection { get; set; }
    BoltConfiguration BoltConfiguration { get; set; }

    Task<bool> EnsureConnection();
    Task StartEventListener(string topic);
    Task AddHandlersFromAssembly<T>();

    Task<HttpStatusCode> InvokeVoidAsync(string methodName, BoltMessage sfMessage);

    Task<BoltRpcResult> InvokeAsync(BoltMessage sfMessage);
}