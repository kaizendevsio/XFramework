using Microsoft.AspNetCore.SignalR.Client;
using XFramework.Domain.Generic.Contracts.Base;
using XFramework.Integration.Abstractions.Wrappers;

namespace XFramework.Integration.Drivers;

public class DriverBase
{
    protected IConfiguration Configuration { get; set; }
    public IMessageBusWrapper MessageBusDriver { get; set; }
    public HubConnectionState ConnectionState => MessageBusDriver.ConnectionState;

    public Guid? TargetClient { get; set; }

    public async Task<CmdResponse> SendVoidAsync<TRequest>(TRequest request) 
        where TRequest : IHasRequestServer, new()
    {
        return await MessageBusDriver.SendVoidAsync<TRequest>(request, TargetClient);
    }
    public async Task<CmdResponse<TRequest>> SendAsync<TRequest>(TRequest request) 
        where TRequest : IHasRequestServer, new()
    {
        return await MessageBusDriver.SendAsync(request, TargetClient);
    }
    public async Task<QueryResponse<TResponse>> SendAsync<TRequest, TResponse>(TRequest request) 
        where TRequest : IHasRequestServer, new()
    {
        return await MessageBusDriver.SendAsync<TRequest, TResponse>(request, TargetClient);
    }
    
}