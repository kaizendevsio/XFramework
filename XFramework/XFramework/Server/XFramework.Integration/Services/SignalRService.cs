using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Domain.Generic.Contracts.Requests;
using TypeSupport.Extensions;
using XFramework.Domain.Generic.Configurations;
using XFramework.Domain.Generic.Enums;
using XFramework.Integration.Entity.Contracts.Responses;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Services.Helpers;

namespace XFramework.Integration.Services;

public class SignalRService : ISignalRService
{
    protected readonly IConfiguration _configuration;
    private readonly IMediator _mediator;
    protected bool _isRegistered;
    protected bool _isRegistering;
    protected List<(string, StreamFlowMessageBO)> _queueList = new();
    protected TaskCompletionSource TaskCompletionSource { get; set; } = new();

    public HubConnection Connection { get; set; }
    public StopWatchHelper StopWatch { get; set; } = new();
    public Stopwatch Stopwatch { get; set; } = new();
    public StreamFlowConfiguration StreamFlowConfiguration { get; set; } = new();

    public ConcurrentDictionary<Guid, TaskCompletionSource<StreamFlowMessageBO>> PendingMethodCalls { get; set; } = new();

    public SignalRService(StreamFlowConfiguration configuration)
    {
        StreamFlowConfiguration = configuration;

        Connection = new HubConnectionBuilder()
            .WithUrl(StreamFlowConfiguration.ServerUrls.First())
            .WithAutomaticReconnect(new[]
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
        Connection.On<string, string>("Ping", (intent, message) => { Console.WriteLine($"Message Received ({DateTime.Now}): [{intent}] {message}"); });

        Handle(_mediator);
        HandleInvokeResponseEvent();
        HandleTelemetryCallEvent();
        HandleReconnectingEvent();
        HandleReconnectedEvent();
        HandleClosedEvent();
    }

    public async Task HandleSubscriptionsEvent(CredentialResponse credentialResponse)
    {
        if (credentialResponse.Guid is null)
        {
            throw new ArgumentException("Handle subscriptions event error: Credential guid is invalid");
        }
        
        var client = new StreamFlowClientBO
        {
            Queue = new()
            {
                Guid = (Guid) credentialResponse.Guid,
            },
        };
        var r =  await Connection.InvokeAsync<HttpStatusCode>("Subscribe", client);
        if (r is not HttpStatusCode.Accepted)
        {
            throw new ArgumentException("Handle subscriptions event error: Failed to subscribe for notifications");
        }

        Console.WriteLine("Started listening to notifications");
    }

    private void HandleInvokeResponseEvent()
    {
        Console.WriteLine($"InvokeResponseHandler Initialized");
        Connection.On<StreamFlowContract>("InvokeResponseHandler",
            async (response) =>
            {
                StopWatch.Start();
                try
                {
                    if (PendingMethodCalls.TryRemove(response.Telemetry.RequestGuid, out TaskCompletionSource<StreamFlowMessageBO> methodCallCompletionSource))
                    {
                        var result = new StreamFlowMessageBO()
                        {
                            ConsumerGuid = response.Telemetry.ConsumerGuid,
                            RequestGuid = response.Telemetry.RequestGuid,
                            Data = response.Data,
                            Message = response.Message,
                            ResponseStatusCode = response.Telemetry.ResponseStatusCode
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
            _isRegistered = false;
            await EnsureConnection();
        };
    }

    private void HandleTelemetryCallEvent()
    {
        Connection.On<string, string>("TelemetryCall", (data, message) => { Console.WriteLine($"Telemetry Call ({DateTime.Now}): {message}"); });
    }

    private void HandleReconnectedEvent()
    {
        Connection.Reconnected += async connectionId =>
        {
            Debug.Assert(Connection.State == HubConnectionState.Connected);
            var request = new StreamFlowClientBO()
            {
                Guid = StreamFlowConfiguration.ClientGuid,
                Name = StreamFlowConfiguration.ClientName
            };
            await Task.Run(() => Connection.InvokeAsync<HttpStatusCode>("Register", request)).ContinueWith(async m =>
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
        if (Connection.State is not HubConnectionState.Disconnected)
        {
            if (_isRegistered) return true;
        } ;

        try
        {
            retry++;
            if (Connection.State is HubConnectionState.Disconnected)
            {
                StopWatch.Start("Connecting to StreamFlow server..");
                await Connection.StartAsync();
                StopWatch.Stop("Connected to StreamFlow server");
            }

            if (_isRegistering)
            {
                Console.WriteLine("Request Postponed, Awaiting Registration..");
                await TaskCompletionSource.Task;
                Console.WriteLine("Request Postponed, Awaiting Registration..");
                return true;
            }

            ;

            Console.WriteLine("Registering Connection..");
            _isRegistering = true;
            var clientId = StreamFlowConfiguration.Anonymous ? Guid.NewGuid() : StreamFlowConfiguration.ClientGuid;
            var request = new StreamFlowClientBO()
            {
                Guid = clientId,
                Name = StreamFlowConfiguration.ClientName
            };
            await Connection.InvokeAsync<HttpStatusCode>("Register", request);
            _isRegistered = true;
            Console.WriteLine("Connection Registered");
            TaskCompletionSource.SetResult();

            return true;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to connect to StreamFlow server: {e.Message} : {e.InnerException?.Message}");
            //if (retry >= StreamFlowConfiguration.MaxRetry) return false;
            Console.WriteLine($"Retrying in {StreamFlowConfiguration.ReconnectDelay}ms");
            await Task.Delay(StreamFlowConfiguration.ReconnectDelay);
            goto RetryConnection;
        }
    }

    public async Task<HttpStatusCode> InvokeVoidAsync(string methodName, StreamFlowMessageBO args1)
    {
        var result = HttpStatusCode.Created;
        StopWatch.Start();
        
        try
        {
            if (Connection.State == HubConnectionState.Reconnecting)
            {
                Console.WriteLine($"Invoked Method '{methodName}' is queued, waiting for connection to be re-established");
                _queueList.Add(new(methodName, args1));
                return HttpStatusCode.Processing;
            }

            if ((_isRegistered == false && methodName != "Register") ||
                (Connection.State is not HubConnectionState.Connected && methodName != "Register"))
            {
                Console.WriteLine($"Method Name: {methodName}");
                await EnsureConnection();
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

    public async Task<SignalRResponse> InvokeAsync(StreamFlowMessageBO args1)
    {
        Stopwatch.Restart();
        var methodCallCompletionSource = new TaskCompletionSource<StreamFlowMessageBO>();

        try
        {
            if (!PendingMethodCalls.TryAdd(args1.RequestGuid, methodCallCompletionSource))
            {
                return new()
                {
                    HttpStatusCode = HttpStatusCode.InternalServerError,
                    Message = $"Error while invoking method '{args1.CommandName}' on {args1.Recipient}"
                };
            }
            var response = methodCallCompletionSource.Task.ConfigureAwait(false);

            ReSendRequest:

            var signalRResponse = await InvokeVoidAsync("Push", args1);
            if (signalRResponse is HttpStatusCode.ServiceUnavailable or HttpStatusCode.NotFound)
            {
                return new()
                {
                    HttpStatusCode = signalRResponse
                };
            }

            new Timer(new((e) =>
                {
                    methodCallCompletionSource.TrySetException(new ArgumentException("Connection timed out"));
                }), null, 300_000, 0);

            Console.WriteLine($"Request Sent: '{args1.CommandName}', awaiting response...");
            try
            {
                var streamFlowMessage = await response;
                Stopwatch.Stop();
                Console.WriteLine($"Response Received: '{args1.CommandName}' => {streamFlowMessage.ResponseStatusCode} ({(streamFlowMessage.IsResponseSuccessful ? "Success" : "Failed")}) ; took {Stopwatch.ElapsedMilliseconds}ms");

                return new()
                {
                    HttpStatusCode = HttpStatusCode.Accepted,
                    Response = streamFlowMessage.Data,
                    Message = streamFlowMessage.Message
                };
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception while awaiting response: {e.Message} : {e.InnerException?.Message}");
                return new()
                {
                    HttpStatusCode = HttpStatusCode.RequestTimeout,
                    Message = $"Error while awaiting response for method '{args1.CommandName}' on {args1.Recipient}"
                };
            }

           
        }
        catch (Exception e)
        {
            Console.WriteLine($"Invoked Method 'push' resulted in Exception: {e.Message} : {e.InnerException?.Message}");
            return new()
            {
                HttpStatusCode = HttpStatusCode.InternalServerError,
                Message = $"Error while invoking method '{args1.CommandName}' on {args1.Recipient}"
            };
        }
    }
}