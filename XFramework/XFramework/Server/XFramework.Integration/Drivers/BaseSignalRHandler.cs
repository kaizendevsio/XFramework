﻿using System.Diagnostics;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Domain.Generic.Contracts.Requests;
using TypeSupport.Extensions;
using XFramework.Integration.Services.Helpers;

namespace XFramework.Integration.Drivers;

public class BaseSignalRHandler
{
    public Guid? Guid { get; set; }
    public Stopwatch Stopwatch { get; set; } = new();

    public async Task<HttpStatusCode> RespondToInvoke<TResult>(HubConnection connection, StreamFlowTelemetryBO telemetry, TResult data) where TResult : new()
    {
        var request = new StreamFlowMessageBO()
        {
            RequestGuid = telemetry.RequestGuid,
            Recipient = telemetry.ClientGuid,
            ExchangeType = MessageExchangeType.Direct,
            ResponseStatusCode = data.GetPropertyValue<HttpStatusCode>("HttpStatusCode")
        };
        request.SetData(data);
        request.CommandName = "InvokeResponseHandler";
        
        return await connection.InvokeAsync<HttpStatusCode>("Push",request);
    }
    
    protected virtual void HandleRequestQuery<TRequest, TQuery, TResponse>(HubConnection connection, IMediator mediator) where TRequest : new() where TResponse : new() where TQuery : IRequest<QueryResponse<TResponse>>
    {
        Console.WriteLine($"{GetType().Name} Initialized");
        connection.On(HandlerName(), async (StreamFlowContract response) => Handler<TRequest, TQuery, QueryResponse<TResponse>>(response, connection, mediator).ConfigureAwait(false));
        //connection.On(HandlerName(), Handler<TRequest, TQuery, QueryResponse<TResponse>>(connection, mediator));

    }
    
    protected virtual void HandleRequestCmd<TRequest, TQuery>(HubConnection connection, IMediator mediator) where TRequest : new() where TQuery : IRequest<CmdResponse<TQuery>>
    {
        Console.WriteLine($"{GetType().Name} Initialized");
        connection.On(HandlerName(), async (StreamFlowContract response) => Handler<TRequest, TQuery, CmdResponse<TQuery>>(response, connection, mediator).ConfigureAwait(false));
        //connection.On(HandlerName(), Handler<TRequest, TQuery, CmdResponse<TQuery>>(connection, mediator));

    }

    protected virtual void HandleVoidRequestCmd<TRequest, TQuery>(HubConnection connection, IMediator mediator) where TRequest : new() where TQuery : IRequest<CmdResponse>
    {
        Console.WriteLine($"{GetType().Name} Initialized");
        connection.On(HandlerName(), async (StreamFlowContract response) => Handler<TRequest, TQuery, CmdResponse>(response, connection, mediator).ConfigureAwait(false));
        //connection.On(HandlerName(), Handler<TRequest, TQuery, CmdResponse>(connection, mediator));
    }

    private async Task Handler<TRequest, TQuery, TResponse>(StreamFlowContract response, HubConnection connection, IMediator mediator) where TRequest : new() where TQuery : IRequest<TResponse> where TResponse : new()
    {
        /*Task.Run(async () =>
            {*/
        Stopwatch.Restart();
        try
        {
            var r = response.Data.AsMediatorCmd<TRequest, TQuery, TResponse>();
            var result = await mediator.Send(r).ConfigureAwait(false);
            Stopwatch.Stop();

            Console.WriteLine(result.GetPropertyValue("HttpStatusCode").ToString() == $"{HttpStatusCode.InternalServerError}"
                ? $"[{DateTime.Now}] Invoked '{GetType().Name}' resulted in exception: [{result.GetType().GetProperty("Message")?.GetValue(result)}] in {Stopwatch.ElapsedMilliseconds}ms"
                : $"[{DateTime.Now}] Invoked '{GetType().Name}' returned {result.GetType().GetProperty("HttpStatusCode")?.GetValue(result)} in {Stopwatch.ElapsedMilliseconds}ms");

            await RespondToInvoke(connection, response.Telemetry, result);
        }
        catch (Exception e)
        {
            Stopwatch.Stop();
            Console.WriteLine($"[{DateTime.Now}] Invoked '{GetType().Name}' resulted in exception: [{e.Message}] in {Stopwatch.ElapsedMilliseconds}ms");
        }
        /*});*/
    }

    private string HandlerName()
    {
        return GetType().Name.Replace("Handler", string.Empty);
    }
}