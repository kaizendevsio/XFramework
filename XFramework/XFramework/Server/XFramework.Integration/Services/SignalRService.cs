using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Domain.Generic.Contracts.Requests;
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
        public Stopwatch Stopwatch { get; set; } = new();
        private StreamFlowConfiguration StreamFlowConfiguration { get; set; } = new();
        public ConcurrentDictionary<Guid, TaskCompletionSource<StreamFlowMessageBO>> PendingMethodCalls { get; set; } = new();
        public SignalRService(StreamFlowConfiguration configuration)
        {
            StreamFlowConfiguration = configuration;

            Connection = new HubConnectionBuilder()
                .WithUrl(StreamFlowConfiguration.ServerUrls.First())
                .WithAutomaticReconnect(new []
                {
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(10),
                })
                .AddMessagePackProtocol()
                .Build();

            HandleEvents();
            Task.Run(async () => await EnsureConnection());
        }

        public SignalRService(IConfiguration configuration, IMediator mediator)
        {
            _configuration = configuration;
            _mediator = mediator;
            configuration.Bind(nameof(StreamFlowConfiguration), StreamFlowConfiguration);

            Connection = new HubConnectionBuilder()
                .WithUrl(StreamFlowConfiguration.ServerUrls.First())
                .WithAutomaticReconnect()
                .AddMessagePackProtocol()
                .Build();

            HandleEvents();
            Task.Run(async () => await EnsureConnection());
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
            HandleInvokeResponseEvent();
            HandleTelemetryCallEvent();
            HandleReconnectingEvent();
            HandleReconnectedEvent();
            HandleClosedEvent();
        }

        private void HandleInvokeResponseEvent()
        {
            Console.WriteLine($"InvokeResponseHandler Initialized");
            Connection.On<string,string,string>("InvokeResponseHandler",
                async (data,message,telemetryString) =>
                {
                    StopWatch.Start();
                    try
                    {
                        var telemetry = JsonSerializer.Deserialize<StreamFlowTelemetryBO>(telemetryString);
                        //Console.WriteLine($"Received InvokeResponseEvent: {telemetry.ClientGuid}");
                        
                        if (PendingMethodCalls.TryRemove(telemetry.RequestGuid, out TaskCompletionSource<StreamFlowMessageBO> methodCallCompletionSource))
                        {
                            var result = new StreamFlowMessageBO()
                            {
                                ConsumerGuid = telemetry.ConsumerGuid,
                                RequestGuid = telemetry.RequestGuid,
                                Data = data,
                                Message = message
                            };
                            await Task.Run(() => methodCallCompletionSource.SetResult(result));
                        }
                        //StopWatch.Stop("Response for Invoked Method Received"); 
                    }
                    catch (Exception e)
                    {
                        StopWatch.Stop($"[{DateTime.Now}] Invoked '{GetType().Name}' resulted in exception {e.Message}"); 
                    }
                 
                });
        }
        
        private void HandleClosedEvent()
        {
            Connection.Closed += async connectionId =>
            {
                Console.WriteLine("Connection to StreamFlow server closed");
                await EnsureConnection();
            };
        }

        private void HandleTelemetryCallEvent()
        {
            Connection.On<string, string>("TelemetryCall",
                (data,message) => { Console.WriteLine($"Telemetry Call ({DateTime.Now}): {message}"); });
        }

        private void HandleReconnectedEvent()
        {
            Connection.Reconnected += async connectionId =>
            {
                Debug.Assert(Connection.State == HubConnectionState.Connected);
                await Task.Run(() => InvokeVoidAsync("Register", new StreamFlowClientBO()
                {
                    Guid = StreamFlowConfiguration.ClientGuid,
                    Name = StreamFlowConfiguration.ClientName
                })).ContinueWith(async m =>
                {
                    _isRegistered = true;
                    // Notify users the connection was reestablished.
                    // Start dequeuing messages queued while reconnecting if any.
                    Console.WriteLine("Connection to StreamFlow server restored");

                    if (!_queueList.Any()) return;

                    Console.WriteLine($"Dequeuing items from cache..");
                    foreach (var valueTuple in _queueList)
                    {
                        InvokeVoidAsync(valueTuple.Item1, valueTuple.Item2);
                    }

                    Console.WriteLine($"Dequeued {_queueList.Count} item(s) from cache");
                    _queueList.Clear();

                });
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
            if (Connection.State is HubConnectionState.Connected or HubConnectionState.Reconnecting) return true;

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

            //StopWatch.Stop($"Invoked Method '{methodName}' returned {result}");
            return result;
        }
        
        public async Task<SignalRResponse> InvokeAsync<T>(T args1)
        {
            Stopwatch.Restart();
            var methodCallCompletionSource = new TaskCompletionSource<StreamFlowMessageBO>();
            var request = args1.Adapt<StreamFlowMessageBO>();
            
            try
            {
                if (!PendingMethodCalls.TryAdd(request.RequestGuid,methodCallCompletionSource))
                {
                    return new()
                    {
                        HttpStatusCode = HttpStatusCode.InternalServerError,
                        Message = $"Error while invoking method '{request.CommandName}' on {request.Recipient}"
                    };
                }
                
                var response = methodCallCompletionSource.Task.ConfigureAwait(false);
                
                var signalRResponse = await InvokeVoidAsync("Push", args1);
                if (signalRResponse is HttpStatusCode.ServiceUnavailable or HttpStatusCode.NotFound)
                {
                    return new ()
                    {
                        HttpStatusCode = signalRResponse
                    };
                }

                Console.WriteLine($"Invoke Method '{request.CommandName}', awaiting response...");
                var streamFlowMessage = await response;
                
                Stopwatch.Stop();
                Console.WriteLine($"Invoked Method '{request.CommandName}' in {Stopwatch.ElapsedMilliseconds}ms");
                
                return new()
                {
                    HttpStatusCode = HttpStatusCode.Accepted,
                    Response = streamFlowMessage.Data,
                    Message = streamFlowMessage.Message
                };

            }
            catch (Exception e)
            {
                Console.WriteLine($"Invoked Method 'push' resulted in Exception: {e.Message} : {e.InnerException?.Message}");
                return new()
                {
                    HttpStatusCode = HttpStatusCode.InternalServerError,
                    Message = $"Error while invoking method '{request.CommandName}' on {request.Recipient}"
                };
            }
        }
        
      
    }
}