using System.Text.Json;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Domain.Generic.Contracts.Requests;
using StreamFlow.Domain.Generic.Enums;

namespace IdentityServer.Api.SignalR.Handlers;

public class BaseSignalRHandler
{
    public StopWatchHelper StopWatch { get; set; } = new();

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
        connection.On<string, string, string>(GetType().Name.Replace("Handler", string.Empty),
            async (data, message, telemetryString) =>
            {
                Task.Run(async () =>
                {
                    StopWatch.Start();
                    try
                    {
                        var telemetry = JsonSerializer.Deserialize<StreamFlowTelemetryBO>(telemetryString);

                        var r = data.AsMediatorCmd<TRequest, TQuery>();
                        var result = await mediator.Send(r).ConfigureAwait(false);
                        StopWatch.Stop($"[{DateTime.Now}] Invoked '{GetType().Name}' returned {result.GetType().GetProperty("HttpStatusCode")?.GetValue(result)}");

                        await RespondToInvoke(connection, telemetry, result);
                    }
                    catch (Exception e)
                    {
                        StopWatch.Stop($"[{DateTime.Now}] Invoked '{GetType().Name}' resulted in exception: [{e.Message}]");
                    }
                });
            });
    }
}