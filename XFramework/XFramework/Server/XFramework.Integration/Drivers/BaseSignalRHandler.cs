using System.Diagnostics;
using System.Text.Json;
using BinaryPack;
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
        var request = new StreamFlowMessageBO(data)
        {
            CommandName = "InvokeResponseHandler",
            RequestGuid = telemetry.RequestGuid,
            Data = BinaryConverter.Serialize(data),
            Recipient = telemetry.ClientGuid,
            ExchangeType = MessageExchangeType.Direct
        };
        return await connection.InvokeAsync<HttpStatusCode>("Push",request);
    }
    
    protected virtual void HandleRequest<TRequest, TQuery>(HubConnection connection, IMediator mediator) where TRequest : new()
    {
        Console.WriteLine($"{GetType().Name} Initialized");
        connection.On(HandlerName(), Handler<TRequest, TQuery>(connection, mediator));
    }

    private string HandlerName()
    {
        return GetType().Name.Replace("Handler", string.Empty);
    }

    private Func<byte[], Task> Handler<TRequest, TQuery>(HubConnection connection, IMediator mediator) where TRequest : new()
    {
        return async (response) =>
        {
            Task.Run(async () =>
            {
                Stopwatch.Restart();
                try
                {
                    var responseObj = BinaryConverter.Deserialize<StreamFlowContract>(response);
                    
                    var r = responseObj.Data.AsMediatorCmd<TRequest, TQuery>();
                    var result = await mediator.Send(r).ConfigureAwait(false);
                    Stopwatch.Stop();

                    Console.WriteLine(result.GetPropertyValue("HttpStatusCode").ToString() == $"{HttpStatusCode.InternalServerError}"
                            ? $"[{DateTime.Now}] Invoked '{GetType().Name}' resulted in exception: [{result.GetType().GetProperty("Message")?.GetValue(result)}] in {Stopwatch.ElapsedMilliseconds}ms"
                            : $"[{DateTime.Now}] Invoked '{GetType().Name}' returned {result.GetType().GetProperty("HttpStatusCode")?.GetValue(result)} in {Stopwatch.ElapsedMilliseconds}ms");

                    await RespondToInvoke(connection, responseObj.Telemetry, result);
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