using Microsoft.AspNetCore.SignalR.Client;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Domain.Generic.Contracts.Requests;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using XFramework.Integration.Services.Helpers;

namespace PaymentGateways.Api.SignalR.Handlers
{
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
                Recipient = telemetry.ClientGuid
            };
            return await connection.InvokeAsync<HttpStatusCode>("Push", request);
        }
    }
}