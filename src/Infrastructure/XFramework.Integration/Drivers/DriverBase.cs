﻿using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Integration.Abstractions.Wrappers;

namespace XFramework.Integration.Drivers;

public record DriverBase(IMessageBusWrapper MessageBusDriver, IConfiguration Configuration)
{
    public DriverBase() : this(null, null)
    {
        
    }
    public HubConnectionState ConnectionState => MessageBusDriver.ConnectionState;

    public virtual void Initialize()
    {
        throw new NotImplementedException();
    }
    
    public string TargetClient { get; set; }

    public async Task<CmdResponse> SendVoidAsync<TRequest>(TRequest request) 
        where TRequest : class, IHasRequestServer
    {
        if (string.IsNullOrEmpty(TargetClient))
        {
            Initialize();
        }
        return await MessageBusDriver.SendVoidAsync<TRequest>(request, TargetClient);
    }
    public async Task<CmdResponse<TResponse>> SendVoidAsync<TRequest, TResponse>(TRequest request) 
        where TRequest : class, IHasRequestServer
    {
        if (string.IsNullOrEmpty(TargetClient))
        {
            Initialize();
        }
        
        var t = await MessageBusDriver.SendVoidAsync<TRequest, TResponse>(request, TargetClient);
        return t;
    }
    public async Task<QueryResponse<TResponse>> SendAsync<TRequest, TResponse>(TRequest request) 
        where TRequest : class, IHasRequestServer
    {
        if (string.IsNullOrEmpty(TargetClient))
        {
            Initialize();
        }
        var t = await MessageBusDriver.SendAsync<TRequest, TResponse>(request, TargetClient);
        return t;
    }
    
}