using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StreamFlow.Domain.Generic.Contracts.Requests;
using TypeSupport.Extensions;
using XFramework.Domain.Generic.Enums;

namespace XFramework.Integration.Drivers;

public class DriverBase
{
    protected IConfiguration Configuration { get; set; }
    public IMessageBusWrapper MessageBusDriver { get; set; }
    public IXLogger Logger { get; set; }
    public HubConnectionState ConnectionState => MessageBusDriver.ConnectionState;

    public Guid? TargetClient { get; set; }

    public async Task<CmdResponse> SendVoidAsync<TRequest>(TRequest request) where TRequest : new()
    {
        return await MessageBusDriver.SendVoidAsync(request, TargetClient);
    }
    public async Task<CmdResponse<TRequest>> SendAsync<TRequest>(TRequest request) where TRequest : new()
    {
        return await MessageBusDriver.SendAsync(request, TargetClient);

    }
    public async Task<QueryResponse<TResponse>> SendAsync<TRequest, TResponse>(TRequest request) where TRequest : new()
    {
        return await MessageBusDriver.SendAsync<TRequest, TResponse>(request, TargetClient);
    }
    
}