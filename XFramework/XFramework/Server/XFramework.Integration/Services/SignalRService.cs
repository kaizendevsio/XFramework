using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using StreamFlow.Domain.BusinessObjects;
using XFramework.Domain.Generic.Configurations;

namespace XFramework.Integration.Services
{
    public class SignalRService
    {
        private readonly IConfiguration _configuration;
        private bool _isRegistered;
        private List<(string, object)> _queueList = new();
        private HubConnection Connection { get; set; }
        private StreamFlowConfiguration StreamFlowConfiguration { get; set; }

        public SignalRService(StreamFlowConfiguration configuration)
        {
            StreamFlowConfiguration = configuration;

            Connection = new HubConnectionBuilder()
                .WithUrl(StreamFlowConfiguration.ServerUrls.First())
                .WithAutomaticReconnect()
                .Build();
        }

        public SignalRService(IConfiguration configuration)
        {
            _configuration = configuration;
            configuration.Bind(nameof(StreamFlowConfiguration), StreamFlowConfiguration);

            Connection = new HubConnectionBuilder()
                .WithUrl(StreamFlowConfiguration.ServerUrls.First())
                .WithAutomaticReconnect()
                .Build();
        }

        private async Task HandleEvents()
        {
            Connection.On<string, string>("Ping", (intent, message) =>
                {
                    Console.WriteLine($"Message Received ({DateTime.Now}): [{intent}] {message}");
                });

            HandleTelemetryCallEvent();
            HandleReconnectingEvent();
            HandleReconnectedEvent();
            HandleClosedEvent();
        }

        private void HandleClosedEvent()
        {
            Connection.Closed += connectionId =>
            {
                Console.WriteLine("Connection to streamFlow server closed");
                Connection.StartAsync();
                return Task.CompletedTask;
            };
        }
        private void HandleTelemetryCallEvent()
        {
            Connection.On<string>("TelemetryCall",
                (message) =>
                {
                    Console.WriteLine($"Telemetry Call ({DateTime.Now}): {message}");
                });
        }
        private void HandleReconnectedEvent()
        {
            Connection.Reconnected += connectionId =>
            {
                Debug.Assert(Connection.State == HubConnectionState.Connected);
                Task.Run(() => InvokeVoidAsync("Register", new StreamFlowClientBO() {Guid = StreamFlowConfiguration.ClientGuid})).Wait();
                _isRegistered = true;
                // Notify users the connection was reestablished.
                // Start dequeuing messages queued while reconnecting if any.
                Console.WriteLine("Connection to streamFlow server restored");

                if (!_queueList.Any()) return Task.CompletedTask;
                
                Console.WriteLine($"Dequeuing items from cache..");
                foreach (var valueTuple in _queueList)
                {
                    InvokeVoidAsync(valueTuple.Item1, valueTuple.Item2);
                }
                Console.WriteLine($"Dequeued {_queueList.Count} item(s) from cache");
                return Task.CompletedTask;
            };
        }
        private void HandleReconnectingEvent()
        {
            Connection.Reconnecting += error =>
            {
                Debug.Assert(Connection.State == HubConnectionState.Reconnecting);
                _isRegistered = false;
                // Notify users the connection was lost and the client is reconnecting.
                // Start queuing or dropping messages.
                Console.WriteLine("Connection to streamFlow server lost, trying to reconnect..");
                return Task.CompletedTask;
            };
        }
        public async Task<bool> EnsureConnection()
        {
            var retry = 0;

            RetryConnection:
            if (!(Connection.State != HubConnectionState.Connected & Connection.State != HubConnectionState.Reconnecting)) return true;
            
            try
            {
                retry++;
                var startTime = DateTime.Now;
                Console.WriteLine("Connecting to streamFlow server..");
                await Connection.StartAsync();
                var endTime = DateTime.Now;
                var elapsedTime = endTime - startTime;
                Console.WriteLine($"Connected to streamFlow server in {elapsedTime.TotalMilliseconds}ms");

                await InvokeVoidAsync("Register", new StreamFlowClientBO(){Guid = StreamFlowConfiguration.ClientGuid});
                _isRegistered = true;
                
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to connect to streamFlow server: {e.Message} : {e.InnerException?.Message}");
                if (retry >= StreamFlowConfiguration.MaxRetry) return false;
                Console.WriteLine($"Retrying in {StreamFlowConfiguration.ReconnectDelay}ms");
                await Task.Delay(StreamFlowConfiguration.ReconnectDelay);
                goto RetryConnection;
            }
        }

        public async Task<HttpStatusCode>InvokeVoidAsync<T>(string methodName, T args1)
        {
            await EnsureConnection();
            var startTime = DateTime.Now;
            var result = HttpStatusCode.Created;
            
            try
            {
                if (Connection.State == HubConnectionState.Reconnecting || (_isRegistered == false & methodName != "Register"))
                {
                    Console.WriteLine($"Invoked Method '{methodName}' is queued, waiting for connection to be re-established");
                    _queueList.Add(new (methodName,args1));
                    return HttpStatusCode.Processing;
                }
                result = await Connection.InvokeAsync<HttpStatusCode>(methodName, args1);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Invoked Method '{methodName}' resulted in Exception: {e.Message} : {e.InnerException?.Message}");
            }

            var endTime = DateTime.Now;
            var elapsedTime = endTime - startTime;

            Console.WriteLine($"Invoked Method '{methodName}' returned {result} in {elapsedTime.TotalMilliseconds}ms");
            return result;
        }
    }
}