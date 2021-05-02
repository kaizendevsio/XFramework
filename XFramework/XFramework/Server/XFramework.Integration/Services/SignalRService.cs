using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.Configurations;
using XFramework.Integration.Entity.Contracts.Responses;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Services.Helpers;

namespace XFramework.Integration.Services
{
    public class SignalRService : ISignalRService
    {

        protected readonly IConfiguration _configuration;
        private readonly IMediator _mediator;
        protected  bool _isRegistered;
        protected  List<(string, object)> _queueList = new();
        public HubConnection Connection { get; set; }
        public StopWatchHelper StopWatch { get; set; } = new();
        private StreamFlowConfiguration StreamFlowConfiguration { get; set; } = new();

        public SignalRService(StreamFlowConfiguration configuration)
        {
            StreamFlowConfiguration = configuration;

            Connection = new HubConnectionBuilder()
                .WithUrl(StreamFlowConfiguration.ServerUrls.First())
                .WithAutomaticReconnect()
                .Build();

            HandleEvents();
            Task.Run(async () => await EnsureConnection()).Wait();
        }

        public SignalRService(IConfiguration configuration, IMediator mediator)
        {
            _configuration = configuration;
            _mediator = mediator;
            configuration.Bind(nameof(StreamFlowConfiguration), StreamFlowConfiguration);

            Connection = new HubConnectionBuilder()
                .WithUrl(StreamFlowConfiguration.ServerUrls.First())
                .WithAutomaticReconnect()
                .Build();

            HandleEvents();
            Task.Run(async () => await EnsureConnection()).Wait();
        }

        public virtual void Handle(IMediator mediator)
        {
        }

        private void HandleEvents()
        {
            Connection.On<string, string>("Ping",
                (intent, message) =>
                {
                    Console.WriteLine($"Message Received ({DateTime.Now}): [{intent}] {message}");
                });
            
            Handle(_mediator);
            HandleTelemetryCallEvent();
            HandleReconnectingEvent();
            HandleReconnectedEvent();
            HandleClosedEvent();
        }

        private void HandleClosedEvent()
        {
            Connection.Closed += connectionId =>
            {
                Console.WriteLine("Connection to StreamFlow server closed");
                Connection.StartAsync();
                return Task.CompletedTask;
            };
        }

        private void HandleTelemetryCallEvent()
        {
            Connection.On<string, string>("TelemetryCall",
                (data,message) => { Console.WriteLine($"Telemetry Call ({DateTime.Now}): {message}"); });
        }

        private void HandleReconnectedEvent()
        {
            Connection.Reconnected += connectionId =>
            {
                Debug.Assert(Connection.State == HubConnectionState.Connected);
                Task.Run(() => InvokeVoidAsync("Register", new StreamFlowClientBO()
                {
                    Guid = StreamFlowConfiguration.ClientGuid,
                    Name = StreamFlowConfiguration.ClientName
                })).Wait();

                _isRegistered = true;
                // Notify users the connection was reestablished.
                // Start dequeuing messages queued while reconnecting if any.
                Console.WriteLine("Connection to StreamFlow server restored");

                if (!_queueList.Any()) return Task.CompletedTask;

                Console.WriteLine($"Dequeuing items from cache..");
                foreach (var valueTuple in _queueList)
                {
                    InvokeVoidAsync(valueTuple.Item1, valueTuple.Item2);
                }

                Console.WriteLine($"Dequeued {_queueList.Count} item(s) from cache");
                _queueList.Clear();

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
                Console.WriteLine("Connection to StreamFlow server lost, trying to reconnect..");
                return Task.CompletedTask;
            };
        }

        public async Task<bool> EnsureConnection()
        {
            var retry = 0;

            RetryConnection:
            if (!(Connection.State != HubConnectionState.Connected &
                  Connection.State != HubConnectionState.Reconnecting)) return true;

            try
            {
                retry++;
                StopWatch.Start("Connecting to StreamFlow server..");
                await Connection.StartAsync();
                StopWatch.Stop("Connected to StreamFlow server");

                await InvokeVoidAsync("Register", new StreamFlowClientBO()
                {
                    Guid = StreamFlowConfiguration.ClientGuid,
                    Name = StreamFlowConfiguration.ClientName
                });
                _isRegistered = true;

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to connect to StreamFlow server: {e.Message} : {e.InnerException?.Message}");
                if (retry >= StreamFlowConfiguration.MaxRetry) return false;
                Console.WriteLine($"Retrying in {StreamFlowConfiguration.ReconnectDelay}ms");
                await Task.Delay(StreamFlowConfiguration.ReconnectDelay);
                goto RetryConnection;
            }
        }

        public async Task<HttpStatusCode> InvokeVoidAsync<T>(string methodName, T args1)
        {
            await EnsureConnection();
            var result = HttpStatusCode.Created;
            StopWatch.Start();
            
            try
            {
                if (Connection.State == HubConnectionState.Reconnecting || (_isRegistered == false & methodName != "Register"))
                {
                    Console.WriteLine($"Invoked Method '{methodName}' is queued, waiting for connection to be re-established");
                    _queueList.Add(new(methodName, args1));
                    return HttpStatusCode.Processing;
                }
                result = await Connection.InvokeAsync<HttpStatusCode>(methodName, args1);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Invoked Method '{methodName}' resulted in Exception: {e.Message} : {e.InnerException?.Message}");
            }

            StopWatch.Stop($"Invoked Method '{methodName}' returned {result}");
            return result;
        }
        
        public async Task<SignalRResponse> InvokeAsync<T>(string methodName, T args1)
        {
            await EnsureConnection();
            StopWatch.Start();
            var result = new SignalRResponse();
            try
            {
                if (Connection.State == HubConnectionState.Reconnecting || (_isRegistered == false & methodName != "Register"))
                {
                    Console.WriteLine($"Invoked Method '{methodName}' is queued, waiting for connection to be re-established");
                    _queueList.Add(new(methodName, args1));
                    return new()
                    {
                        HttpStatusCode = HttpStatusCode.Processing
                    };
                }
                result = await Connection.InvokeAsync<SignalRResponse>(methodName, args1);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Invoked Method '{methodName}' resulted in Exception: {e.Message} : {e.InnerException?.Message}");
            }

            StopWatch.Stop($"Invoked Method '{methodName}' returned {result}");
            return result;
        }
        
      
    }
}