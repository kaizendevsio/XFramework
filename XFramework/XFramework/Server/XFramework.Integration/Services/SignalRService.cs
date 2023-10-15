using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StreamFlow.Domain.Generic.Abstractions;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Domain.Generic.Contracts.Requests;
using XFramework.Domain.Generic.Configurations;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Services.Helpers;

namespace XFramework.Integration.Services;

public class SignalRService : ISignalRService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SignalRService> _logger;
    private readonly MetricsMonitor _metricsMonitor;
    private bool _isRegistered;
    private bool _isRegistering;
    private bool _subscriptionsEventHandle;

    private readonly List<(string MethodName, StreamFlowMessage StreamFlowMessage)> _queueList = new();
    protected TaskCompletionSource TaskCompletionSource { get; set; } = new();
    public HubConnection? Connection { get; set; }
    
    public StreamFlowConfiguration StreamFlowConfiguration { get; set; } = new();
    public ConcurrentDictionary<Guid, TaskCompletionSource<StreamFlowMessage>> PendingMethodCalls { get; set; } = new();

    public SignalRService(IConfiguration configuration, ILogger<SignalRService> logger, MetricsMonitor metricsMonitor)
    {
        _metricsMonitor = metricsMonitor;
        _configuration = configuration;
        _logger = logger;
        configuration.Bind(nameof(StreamFlowConfiguration), StreamFlowConfiguration);
        
        InitializeService();
    }

    private void InitializeService()
    {
        if (StreamFlowConfiguration.ServerUrls is null || !StreamFlowConfiguration.ServerUrls.Any())
        {
            _logger.LogWarning("StreamFlow configuration is not set, therefore SignalR client service is disabled");
            return;
        }
        
        Connection = new HubConnectionBuilder()
            .WithUrl(StreamFlowConfiguration.ServerUrls.First())
            .WithAutomaticReconnect(Enumerable.Repeat(TimeSpan.FromSeconds(2), 2000).ToArray())
            .AddMessagePackProtocol()
            .Build();

        HandleEvents();
        Task.Run(async () => await EnsureConnection());
    }

    private void HandleEvents()
    {
        Connection?.On<string, string>("Ping", (intent, message) => { _logger.LogInformation("Message Received ({Now}): [{Intent}] {Message}", DateTime.Now, intent, message); });

        HandleInvokeResponseEvent();
        HandleTelemetryCallEvent();
        HandleReconnectingEvent();
        HandleReconnectedEvent();
        HandleClosedEvent();
    }

    public async Task StartEventListener(string topic)
    {
        if (_subscriptionsEventHandle) return;
        if (string.IsNullOrEmpty(topic)) return;

        _subscriptionsEventHandle = true;
        var client = new StreamFlowClient
        {
            Queue = new()
            {
                Name = topic
            },
        };
        var r = await Connection?.InvokeAsync<HttpStatusCode>(nameof(IStreamFlow.Subscribe), client);
        if (r is not HttpStatusCode.Accepted)
        {
            throw new ArgumentException("Handle subscriptions event error: Failed to subscribe for notifications");
        }

        _logger.LogInformation("Notification listener started");
    }

    private void HandleInvokeResponseEvent()
    {
        //_logger.LogInformation($"InvokeResponseHandler Initialized");
        Connection?.On<StreamFlowMessage>(nameof(IStreamFlow.InvokeResponse),
            async (response) =>
            {
                using var metricLogger = _metricsMonitor.Start($"Invoked '{GetType().Name}'");
                try
                {
                    if (PendingMethodCalls.TryRemove(response.RequestId, out TaskCompletionSource<StreamFlowMessage> methodCallCompletionSource))
                    {
                        var result = new StreamFlowMessage()
                        {
                            ConsumerId = response.ConsumerId,
                            RequestId = response.RequestId,
                            Data = response.Data,
                            Message = response.Message,
                            ResponseStatusCode = response.ResponseStatusCode
                        };
                        await Task.Run(() => methodCallCompletionSource.SetResult(result));
                    }
                    //StopWatch.Stop("Response for Invoked Method Received"); 
                }
                catch (Exception e)
                {
                    metricLogger.Failed($"Invoked '{GetType().Name}' resulted in exception {e.Message}");
                }
            });
    }

    private void HandleClosedEvent()
    {
        Connection.Closed += async connectionId =>
        {
            _logger.LogInformation("Connection to StreamFlow server closed");
            _isRegistered = false;
            await EnsureConnection();
        };
    }

    private void HandleTelemetryCallEvent()
    {
        Connection?.On<string, string>(nameof(IStreamFlow.TelemetryCall), (data, message) => { _logger.LogInformation("Telemetry Call ({Now}): {Message}", DateTime.Now, message); });
    }

    private void HandleReconnectedEvent()
    {
        if (Connection == null)
        {
            _logger.LogInformation("Cannot handle reconnected event, connection is null");
            return;
        }
        
        Connection.Reconnected += async connectionId =>
        {
            Debug.Assert(Connection?.State == HubConnectionState.Connected);

            // Notify users the connection was reestablished.
            // Start dequeuing messages queued while reconnecting if any.

            _logger.LogInformation("Connection to StreamFlow server restored");

            await RegisterConnection();

            if (!_queueList.Any()) return;

            _logger.LogInformation($"Dequeuing items from cache..");

            foreach (var valueTuple in _queueList)
            {
                // Awaiting to preserve transactional order
                await InvokeVoidAsync(valueTuple.MethodName, valueTuple.StreamFlowMessage);
            }

            _logger.LogInformation("Dequeued {QueueListCount} item(s) from cache", _queueList.Count);
            _queueList.Clear();
        };
    }

    private void HandleReconnectingEvent()
    {
        if (Connection == null)
        {
            _logger.LogInformation("Cannot handle reconnecting event, connection is null");
            return;
        }
        
        Connection.Reconnecting += error =>
        {
            // Notify users the connection was lost and the client is reconnecting.
            // Start queuing or dropping messages.
            Debug.Assert(Connection?.State == HubConnectionState.Reconnecting);
            _isRegistered = false;
            _isRegistering = false;
            
            _logger.LogInformation("Connection to StreamFlow server lost, trying to reconnect..");
            //EnsureConnection();
            
            return Task.CompletedTask;
        };
    }

    public async Task<bool> EnsureConnection()
    {
        const int maxRetries = 5;

        for (int retry = 0; retry < maxRetries; retry++)
        {
            try
            {
                // Check if connection is already established and registered.
                if (Connection?.State is not HubConnectionState.Disconnected && _isRegistered)
                {
                    return true;
                }

                // If connection is in a reconnecting state, wait for it.
                if (Connection?.State == HubConnectionState.Reconnecting)
                {
                    _logger.LogInformation("Connection is in the process of reconnecting, waiting...");
                    return true;
                }

                // If we're in the process of registering, then wait for it.
                if (_isRegistering)
                {
                    _logger.LogInformation("Request Postponed, Awaiting Registration..");
                    return true;
                }

                // If connection is disconnected, then start it.
                if (Connection?.State == HubConnectionState.Disconnected)
                {
                    using var metricLogger = _metricsMonitor.Start("Connecting to StreamFlow server..");
                    await Connection.StartAsync();
                    metricLogger.Completed();
                }

                // If we're connected, proceed with registration.
                if (Connection?.State == HubConnectionState.Connected)
                {
                    await RegisterConnection();
                }

                return true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to connect to StreamFlow server: {EMessage} : {InnerExceptionMessage}", e.Message, e.InnerException?.Message);
                _logger.LogInformation("Retrying in {ReconnectDelay}ms", StreamFlowConfiguration.ReconnectDelay);
                await Task.Delay(StreamFlowConfiguration.ReconnectDelay);
            }
        }

        return false;
    }

    private async Task RegisterConnection()
    {
        if(_isRegistered) return;
        
        _isRegistering = true;
        using (_metricsMonitor.Start("Registering Connection.."))
        {
            var clientId = StreamFlowConfiguration.Anonymous ? Guid.NewGuid() : StreamFlowConfiguration.ClientGuid;
            var request = new StreamFlowClient()
            {
                Guid = clientId ?? Guid.Empty,
                Name = StreamFlowConfiguration.ClientName
            };
            await Connection?.InvokeAsync<HttpStatusCode>(nameof(IStreamFlow.Register), request);
        
            _isRegistered = true;
            _isRegistering = false;
        }
    }

    public async Task<HttpStatusCode> InvokeVoidAsync(string methodName, StreamFlowMessage sfMessage)
    {
        _metricsMonitor.Start();
        try
        {
            if (Connection?.State is HubConnectionState.Connected && _isRegistered is true && !_isRegistering)
                return await Connection?.InvokeAsync<HttpStatusCode>(methodName, sfMessage);
            
            _logger.LogInformation("Invoked Method \'{MethodName}\' is queued, waiting for connection to be re-established", methodName);
            _queueList.Add(new(methodName, sfMessage));
            return HttpStatusCode.Processing;

        }
        catch (Exception e)
        {
            _logger.LogError("Invoked Method \'{MethodName}\' resulted in Exception: {EMessage} : {InnerExceptionMessage}", methodName, e.Message, e.InnerException?.Message);
        }
        return HttpStatusCode.InternalServerError;
    }

    public async Task<StreamFlowMessage> InvokeAsync(StreamFlowMessage sfMessage)
    {
        using var metricLogger = _metricsMonitor.Start();

        var methodCallCompletionSource = new TaskCompletionSource<StreamFlowMessage>();

        try
        {
            if (!PendingMethodCalls.TryAdd(sfMessage.RequestId, methodCallCompletionSource))
            {
                return CreateErrorResponse(sfMessage, HttpStatusCode.InternalServerError,
                    $"Error while invoking method '{sfMessage.CommandName}' on {sfMessage.RecipientId}");
            }

            var response = methodCallCompletionSource.Task.ConfigureAwait(false);

            var signalRResponse = await InvokeVoidAsync("Push", sfMessage);
            if (signalRResponse is HttpStatusCode.ServiceUnavailable or HttpStatusCode.NotFound)
            {
                return new()
                {
                    ResponseStatusCode = signalRResponse
                };
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300_000));
            cts.Token.Register(() => methodCallCompletionSource.TrySetException(new ArgumentException("Connection timed out")));

            _logger.LogInformation("Request Sent: \'{SfMessageCommandName}\', awaiting response...", sfMessage.CommandName);

            try
            {
                var streamFlowMessage = await response;
                metricLogger.Completed($"Response for Invoked Method '{sfMessage.CommandName}' Received with status code {streamFlowMessage.ResponseStatusCode}");

                return new()
                {
                    ResponseStatusCode = HttpStatusCode.Accepted,
                    Data = streamFlowMessage.Data,
                    Message = streamFlowMessage.Message
                };
            }
            catch (Exception e)
            {
                _logger.LogError("Exception while awaiting response: {EMessage} : {InnerExceptionMessage}", e.Message, e.InnerException?.Message);
                return CreateErrorResponse(sfMessage, HttpStatusCode.RequestTimeout,
                    $"Error while awaiting response for method '{sfMessage.CommandName}' on {sfMessage.RecipientId}");
            }
        }
        catch (Exception e)
        {
            _logger.LogError("Invoked Method \'push\' resulted in Exception: {EMessage} : {InnerExceptionMessage}", e.Message, e.InnerException?.Message);
            return CreateErrorResponse(sfMessage, HttpStatusCode.InternalServerError,
                $"Error while invoking method '{sfMessage.CommandName}' on {sfMessage.RecipientId}");
        }
    }

    private StreamFlowMessage CreateErrorResponse(StreamFlowMessage sfMessage, HttpStatusCode code, string message)
    {
        return new()
        {
            ResponseStatusCode = code,
            Message = message
        };
    }

}