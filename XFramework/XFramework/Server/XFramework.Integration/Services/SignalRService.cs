using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using HashidsNet;
using IdentityServer.Domain.Generic.Contracts.Requests;
using IdentityServer.Domain.Generic.Contracts.Responses;
using MediatR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StreamFlow.Domain.Generic.Abstractions;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Domain.Generic.Contracts.Requests;
using TypeSupport.Extensions;
using XFramework.Domain.Generic.Configurations;
using XFramework.Domain.Generic.Contracts.Base;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Drivers;
using XFramework.Integration.Security;

namespace XFramework.Integration.Services;

public class SignalRService : BaseSignalRHandler, ISignalRService
{
    private readonly ILogger<SignalRService> _logger;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IConfiguration _configuration;
    private readonly IMediator _mediator;
    private readonly ILogger<BaseSignalRHandler> _baseLogger;
    private readonly IServiceScopeFactory _scopeFactory;
    private string _clientId;
    private bool _isRegistered;
    private bool _isRegistering;
    private bool _subscriptionsEventHandle;

    private readonly List<(string MethodName, StreamFlowMessage  StreamFlowMessage)> _queueList = new();
    protected TaskCompletionSource TaskCompletionSource { get; set; } = new();
    public HubConnection? Connection { get; set; }
    
    public StreamFlowConfiguration StreamFlowConfiguration { get; set; } = new();
    public ConcurrentDictionary<Guid, TaskCompletionSource<StreamFlowMessage>> PendingMethodCalls { get; set; } = new();

    public SignalRService(IHostEnvironment hostEnvironment, IConfiguration configuration, ILogger<SignalRService> logger, IMediator mediator, ILogger<BaseSignalRHandler> baseLogger, IServiceScopeFactory scopeFactory)
    {
        _hostEnvironment = hostEnvironment;
        _configuration = configuration;
        _mediator = mediator;
        _baseLogger = baseLogger;
        _scopeFactory = scopeFactory;
        _logger = logger;
        configuration.Bind(nameof(StreamFlowConfiguration), StreamFlowConfiguration);
        
        InitializeService();
    }

    private void InitializeService()
    {
        var envConfig = _configuration["STREAMFLOW_SERVER_URLS"];
        
        if ((StreamFlowConfiguration.ServerUrls is null || !StreamFlowConfiguration.ServerUrls.Any()) && string.IsNullOrEmpty(envConfig))
        {
            _logger.LogWarning("StreamFlow configuration is not set, therefore SignalR client service is disabled");
            return;
        }
        
        Connection = new HubConnectionBuilder()
            .WithUrl(StreamFlowConfiguration?.ServerUrls?.FirstOrDefault() ?? new Uri(envConfig))
            .WithAutomaticReconnect(Enumerable.Repeat(TimeSpan.FromSeconds(2), 2000).ToArray())
            .AddMessagePackProtocol()
            .Build();
        
        HandleEvents();
        Task.Run(async () => await EnsureConnection());
    }

    private void HandleEvents()
    {
        Connection?.On<string, string>("Ping", (intent, message) => { _logger.LogInformation("Message Received ({Now}): [{Intent}] {Message}", DateTime.Now, intent, message); });

        ScanAndRegisterHandlers();
        HandleInvokeResponseEvent();
        HandleTelemetryCallEvent();
        HandleReconnectingEvent();
        HandleReconnectedEvent();
        HandleClosedEvent();
    }

    public Task AddHandlersFromAssembly<T>()
    {
        var typesImplementingStreamflowRequest = Assembly.GetAssembly(typeof(T)).GetTypes()
            .Where(x => !x.IsInterface && !x.IsAbstract)
            .SelectMany(x => x.GetInterfaces(), (x, i) => new { Type = x, Interface = i })
            .Where(x => x.Interface.IsGenericType && x.Interface.GetGenericTypeDefinition() == typeof(IStreamflowRequest<,>))
            .ToList();

        foreach (var type in typesImplementingStreamflowRequest)
        {
            var genericArguments = type.Interface.GetGenericArguments();

            // Ensure there are exactly two generic arguments (TRequest, TResponse)
            if (genericArguments.Length == 2)
            {
                Type tRequest = genericArguments[0];
                Type tResponse = genericArguments[1];

                // Now you have TRequest and TResponse for each type that implements IStreamflowRequest<TRequest, TResponse>
                // You can process them as needed, for example:
                Console.WriteLine($"Type: {type.Type.Name}, TRequest: {tRequest.Name}, TResponse: {tResponse.Name}");
        
                // If you need to invoke HandleRequestCmd or other methods dynamically, you can do so here.

                if (tResponse.IsAssignableTo(typeof(ICmdResponse)))
                {
                    // Use reflection to call HandleRequestCmd with the correct type arguments
                    var methodInfo = GetType()
                        .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                        .First(m => m.Name == nameof(HandleRequestCmd) && m.GetGenericArguments().Length == 1);
                    
                    var genericMethod = methodInfo.MakeGenericMethod(tRequest);
                    genericMethod.Invoke(this, new object[] { Connection, _mediator, _baseLogger, _scopeFactory });
                }
                else if (tResponse.IsAssignableTo(typeof(IQueryResponse)))
                {
                    var methodInfo = GetType().GetMethod(nameof(HandleRequestQuery), BindingFlags.NonPublic | BindingFlags.Instance);
                    var genericMethod = methodInfo.MakeGenericMethod(tRequest, tResponse.GetGenericArguments().First());
                    genericMethod.Invoke(this, new object[] { Connection, _mediator, _baseLogger, _scopeFactory });
                }
            }
        }
        return Task.CompletedTask;
    }

    private void ScanAndRegisterHandlers()
    {
        var installers = Assembly.GetEntryAssembly().ExportedTypes
            .Where(x => typeof(ISignalREventHandler).IsAssignableFrom(x) && x is { IsInterface: false, IsAbstract: false })
            .Select(Activator.CreateInstance)
            .Cast<ISignalREventHandler>()
            .ToList();
        
        installers.ForEach(installer => installer.Handle(Connection, _mediator, _baseLogger, _scopeFactory));
    }

    public async Task StartEventListener(string topic)
    {
        if (string.IsNullOrEmpty(topic)) return;
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

        if (!_hostEnvironment.IsProduction())
        {
            _logger.LogInformation("Started subscription event listener with topic {Topic}", topic);
        }
    }

    private void HandleInvokeResponseEvent()
    {
        //_logger.LogInformation($"InvokeResponseHandler Initialized");
        Connection?.On<StreamFlowMessage>(nameof(IStreamFlow.InvokeResponseHandler),
            async (response) =>
            {
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
                    _logger.LogInformation("[{Caller}] Processing response for '{Request}' resulted in exception: {Error}", nameof(HandleInvokeResponseEvent), response.CommandName, e.Message);
                }
            });
    }

    private void HandleClosedEvent()
    {
        Connection!.Closed += async connectionId =>
        {
            _logger.LogInformation("Connection to StreamFlow server close, connectionId: {ConnectionId}", connectionId);
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
                    var startTimer = Stopwatch.StartNew();
                    _logger.LogInformation("Connecting to StreamFlow server..");

                    await Connection.StartAsync();
                    startTimer.Stop();
                    _logger.LogInformation("Connecting to StreamFlow server.. Done in {ElapsedMilliseconds}ms", startTimer.ElapsedMilliseconds);
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
        var startTimer = Stopwatch.StartNew();
        _logger.LogInformation("Registering Connection..");
        
        var serviceName = Assembly.GetEntryAssembly()?.GetName().Name.Split(".").First() ?? throw new ArgumentException("Assembly name is not set");
        var serviceId = serviceName.ToSha256();
        
        _clientId = StreamFlowConfiguration.Anonymous ? $"sfc_{Guid.NewGuid()}" : serviceId ?? throw new ArgumentException("Streamflow client Id is not set");
        _logger.LogInformation("Registering streamflow client with id {ClientId}", _clientId);
        
        var request = new StreamFlowClient()
        {
            Id = _clientId,
            Name = StreamFlowConfiguration.ClientName
        };
        await Connection?.InvokeAsync<HttpStatusCode>(nameof(IStreamFlow.Register), request);
    
        startTimer.Stop();
        _logger.LogInformation("Registering Connection.. Done in {ElapsedMilliseconds}ms", startTimer.ElapsedMilliseconds);
        
        _isRegistered = true;
        _isRegistering = false;
    }

    public async Task<HttpStatusCode> InvokeVoidAsync(string methodName, StreamFlowMessage sfMessage) 
    {
        try
        {
            if (Connection?.State is HubConnectionState.Connected && _isRegistered is true && !_isRegistering)
            {
                var y = sfMessage.Adapt<StreamFlowMessage>();
                return await Connection?.InvokeAsync<HttpStatusCode>(methodName, y);
            }
            
            _logger.LogInformation("Invoked Method \'{MethodName}\' is queued, waiting for connection to be re-established", methodName);
            _queueList.Add(new(methodName, sfMessage as StreamFlowMessage ));
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
        var startTimer = Stopwatch.StartNew();
        sfMessage.ClientId = _clientId;
        
        _logger.LogWarning("Invoking Method \'{SfMessageCommandName}\' on {SfMessageRecipientId}", sfMessage.CommandName, sfMessage.RecipientId);
        
        var methodCallCompletionSource = new TaskCompletionSource<StreamFlowMessage>();

        try
        {
            if (!PendingMethodCalls.TryAdd(sfMessage.RequestId, methodCallCompletionSource))
            {
                return CreateErrorResponse(sfMessage, HttpStatusCode.InternalServerError, $"Error while invoking method '{sfMessage.CommandName}' on {sfMessage.RecipientId}");
            }

            var response = methodCallCompletionSource.Task.ConfigureAwait(false);
            var signalRResponse = await InvokeVoidAsync(nameof(IStreamFlow.Push), sfMessage);
            
            if (signalRResponse is HttpStatusCode.ServiceUnavailable or HttpStatusCode.NotFound)
            {
                startTimer.Stop();
                _logger.LogWarning("Invoked Method \'{SfMessageCommandName}\' resulted in {ResponseStatusCode} response", sfMessage.CommandName, signalRResponse);
                return new()
                {
                    ResponseStatusCode = signalRResponse
                };
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300_000));
            cts.Token.Register(() => methodCallCompletionSource.TrySetException(new ArgumentException("Connection timed out")));

            _logger.LogWarning("Awaiting response for Invoked Method \'{SfMessageCommandName}\'", sfMessage.CommandName);

            try
            {
                var streamFlowMessage = await response;
                
                startTimer.Stop();
                _logger.LogWarning("Response for Invoked Method \'{SfMessageCommandName}\' received in {ElapsedMilliseconds}ms", sfMessage.CommandName, startTimer.ElapsedMilliseconds);

                return new()
                {
                    ResponseStatusCode = HttpStatusCode.Accepted,
                    Data = streamFlowMessage.Data,
                    Message = streamFlowMessage.Message,
                    Duration = startTimer.Elapsed
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
            return CreateErrorResponse(sfMessage, HttpStatusCode.InternalServerError, $"Error while invoking method '{sfMessage.CommandName}' on {sfMessage.RecipientId}");
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