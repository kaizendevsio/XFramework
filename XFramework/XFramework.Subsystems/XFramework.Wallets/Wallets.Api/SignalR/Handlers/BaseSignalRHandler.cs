using System.Diagnostics;
using System.Net;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Domain.Generic.Contracts.Requests;
using StreamFlow.Domain.Generic.Enums;
using TypeSupport.Extensions;
using XFramework.Integration.Services.Helpers;

namespace Wallets.Api.SignalR.Handlers;

public class BaseSignalRHandler
{
    public Guid? Guid { get; set; }
    public Stopwatch Stopwatch { get; set; } = new();

    public async Task<HttpStatusCode> RespondToInvoke<TResult>(HubConnection connection, StreamFlowTelemetryBO telemetry, TResult data)
    {
        var request = new StreamFlowMessageBO(data)
        {
            CommandName = "InvokeResponseHandler",
            RequestGuid = telemetry.RequestGuid,
            Data = JsonSerializer.Serialize(data),
            Recipient = telemetry.ClientGuid,
            ExchangeType = MessageExchangeType.Direct
        };
        return await connection.InvokeAsync<HttpStatusCode>("Push",request);
    }
    
    protected virtual void HandleRequest<TRequest, TQuery>(HubConnection connection, IMediator mediator)
    {
        Console.WriteLine($"{GetType().Name} Initialized");
        connection.On(HandlerName(), Handler<TRequest, TQuery>(connection, mediator));
    }

    private string HandlerName()
    {
        return GetType().Name.Replace("Handler", string.Empty);
    }

    private Func<string, string, string, Task> Handler<TRequest, TQuery>(HubConnection connection, IMediator mediator)
    {
        return async (data, message, telemetryString) =>
        {
            Task.Run(async () =>
            {
                Stopwatch.Restart();
                try
                {
                    var telemetry = JsonSerializer.Deserialize<StreamFlowTelemetryBO>(telemetryString);
                    
                    var r = data.AsMediatorCmd<TRequest, TQuery>();
                    var result = await mediator.Send(r).ConfigureAwait(false);
                    Stopwatch.Stop();

                    Console.WriteLine(result.GetPropertyValue("HttpStatusCode").ToString() == $"{HttpStatusCode.InternalServerError}"
                            ? $"[{DateTime.Now}] Invoked '{GetType().Name}' resulted in exception: [{result.GetType().GetProperty("Message")?.GetValue(result)}] in {Stopwatch.ElapsedMilliseconds}ms"
                            : $"[{DateTime.Now}] Invoked '{GetType().Name}' returned {result.GetType().GetProperty("HttpStatusCode")?.GetValue(result)} in {Stopwatch.ElapsedMilliseconds}ms");

                    await RespondToInvoke(connection, telemetry, result);
                }
                catch (Exception e)
                {
                    Stopwatch.Stop();
                    Console.WriteLine($"[{DateTime.Now}] Invoked '{GetType().Name}' resulted in exception: [{e.Message}] in {Stopwatch.ElapsedMilliseconds}ms");
                }
            });
        };
    }
}