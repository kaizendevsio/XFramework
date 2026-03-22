using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using HashidsNet;
using IdentityServer.Domain.Shared.Contracts.Requests;
using IdentityServer.Domain.Shared.Contracts.Responses;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StreamFlow.Domain.Shared.Abstractions;
using StreamFlow.Domain.Shared.BusinessObjects;
using StreamFlow.Domain.Shared.Contracts.Requests;
using StreamFlow.Domain.Shared.Contracts.Responses;
using TypeSupport.Extensions;
using XFramework.Domain.Shared.Configurations;
using XFramework.Domain.Shared.Contracts.Base;
using XFramework.Integration.Abstractions;
using XFramework.Integration.Drivers;
using XFramework.Integration.Security;

namespace XFramework.Integration.Services;

public class SignalRService : BaseSignalRHandler, ISignalRService
{
    private readonly ILogger<SignalRService> _logger;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BaseSignalRHandler> _baseLogger;
    private readonly IServiceScopeFactory _scopeFactory;
    private string _clientId;
    private bool _isRegistered;
    private bool _isRegistering;
    private bool _subscriptionsEventHandle;

    private readonly ConcurrentQueue<(string MethodName, StreamFlowMessage StreamFlowMessage)> _offlineQueue = new();
    protected TaskCompletionSource TaskCompletionSource { get; set; } = new();
    public HubConnection? Connection { get; set; }
    
    public StreamFlowConfiguration StreamFlowConfiguration { get; set; } = new();
    public ConcurrentDictionary<Guid, PooledRpcCall> PendingMethodCalls { get; set; } = new();

    public SignalRService(IHostEnvironment hostEnvironment, IConfiguration configuration, ILogger<SignalRService> logger, ILogger<BaseSignalRHandler> baseLogger, IServiceScopeFactory scopeFactory)
    {
        _hostEnvironment = hostEnvironment;
        _configuration = configuration;
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

        // Environment variable takes precedence (Docker override), then appsettings config
        var serverUrl = !string.IsNullOrEmpty(envConfig)
            ? new Uri(envConfig)
            : StreamFlowConfiguration?.ServerUrls?.FirstOrDefault();
        Connection = new HubConnectionBuilder()
            .WithUrl(serverUrl, (opts) =>
            {
                if (OperatingSystem.IsBrowser()) return;
                
                if (serverUrl.AbsoluteUri.StartsWith("https://localhost", StringComparison.OrdinalIgnoreCase)
                    || serverUrl.AbsoluteUri.StartsWith("https://127.0.0.1", StringComparison.OrdinalIgnoreCase))
                {
                    opts.HttpMessageHandlerFactory = (message) =>
                    {
                        if (message is HttpClientHandler clientHandler)
                            // always verify the SSL certificate
                            clientHandler.ServerCertificateCustomValidationCallback += (sender, certificate, chain, sslPolicyErrors) => true;
                        return message;
                    };
                }
            })
            .WithAutomaticReconnect(Enumerable.Repeat(TimeSpan.FromSeconds(2), 2000).ToArray())
            .AddMessagePackProtocol(options =>
            {
                options.SerializerOptions = MessagePack.MessagePackSerializerOptions.Standard
                    .WithCompression(MessagePack.MessagePackCompression.Lz4BlockArray)
                    .WithSecurity(MessagePack.MessagePackSecurity.UntrustedData)
                    .WithResolver(MessagePack.Resolvers.ContractlessStandardResolver.Instance);
            })
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

                if (tResponse.IsAssignableTo(typeof(ICmdWithResultResponse)))
                {
                    // Use reflection to call HandleRequestCmd with the correct type arguments
                    var methodInfo = GetType()
                        .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                        .First(m => m.Name == nameof(HandleRequestCmd) && m.GetGenericArguments().Length == 2);
                    
                    var genericMethod = methodInfo.MakeGenericMethod(tRequest, tResponse.GetGenericArguments().First());
                    genericMethod.Invoke(this, [Connection, _baseLogger, _scopeFactory]);
                }
                else if (tResponse.IsAssignableTo(typeof(ICmdResponse)))
                {
                    // Use reflection to call HandleRequestCmd with the correct type arguments
                    var methodInfo = GetType()
                        .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                        .First(m => m.Name == nameof(HandleRequestCmd) && m.GetGenericArguments().Length == 1);
                    
                    var genericMethod = methodInfo.MakeGenericMethod(tRequest);
                    genericMethod.Invoke(this, [Connection, _baseLogger, _scopeFactory]);
                }
                else if (tResponse.IsAssignableTo(typeof(IQueryResponse)))
                {
                    var methodInfo = GetType().GetMethod(nameof(HandleRequestQuery), BindingFlags.NonPublic | BindingFlags.Instance);
                    var genericMethod = methodInfo.MakeGenericMethod(tRequest, tResponse.GetGenericArguments().First());
                    genericMethod.Invoke(this, [Connection, _baseLogger, _scopeFactory]);
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
        
        installers.ForEach(installer => installer.Handle(Connection, _baseLogger, _scopeFactory));
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
                    if (PendingMethodCalls.TryRemove(response.RequestId, out var rpcCall))
                    {
                        var result = new StreamFlowMessage()
                        {
                            ConsumerId = response.ConsumerId,
                            RequestId = response.RequestId,
                            Data = response.Data,
                            Message = response.Message,
                            ResponseStatusCode = response.ResponseStatusCode
                        };
                        rpcCall.SetResult(result);
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
            _logger.LogInformation("Connection to StreamFlow server closed, connectionId: {ConnectionId}", connectionId);

            // Cancel all pending RPCs to prevent orphaned calls
            foreach (var (id, _) in PendingMethodCalls)
            {
                if (PendingMethodCalls.TryRemove(id, out var rpcCall))
                    rpcCall.SetException(new InvalidOperationException("Connection lost"));
            }

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
                    _logger.LogInformation("Connecting to StreamFlow server.. Done in {ResponseTime}ms", startTimer.ElapsedMilliseconds);
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
        
        var serviceName = !string.IsNullOrEmpty(StreamFlowConfiguration.ClientName)
            ? StreamFlowConfiguration.ClientName.Split(".").First()
            : Assembly.GetEntryAssembly()!.GetName().Name!.Split(".").First()
              ?? throw new ArgumentException("Assembly name is not set");
        var serviceId = serviceName.ToSha256();
        
        _clientId = StreamFlowConfiguration.Anonymous ? $"sfc_{Guid.NewGuid()}" : serviceId ?? throw new ArgumentException("Streamflow client Id is not set");
        _logger.LogInformation("Registering streamflow client with id {ClientId}", _clientId);
        
        var request = new StreamFlowClient()
        {
            Id = _clientId,
            Name = StreamFlowConfiguration.ClientName,
            Queue = new StreamFlowQueue()
        };
        await Connection!.InvokeAsync<HttpStatusCode>(nameof(IStreamFlow.Register), request);
    
        startTimer.Stop();
        _logger.LogInformation("Registering Connection.. Done in {ResponseTime}ms", startTimer.ElapsedMilliseconds);
        
        _isRegistered = true;
        _isRegistering = false;
        
        if (_offlineQueue.IsEmpty) return;

        _logger.LogInformation("Dequeuing items from cache..");

        var dequeued = 0;
        while (_offlineQueue.TryDequeue(out var item))
        {
            var sent = false;
            for (int attempt = 0; attempt < 3 && !sent; attempt++)
            {
                try
                {
                    await Connection.InvokeAsync<HttpStatusCode>(item.MethodName, item.StreamFlowMessage);
                    sent = true;
                }
                catch (Exception ex) when (attempt < 2)
                {
                    _logger.LogWarning("Retry {Attempt}/3 for offline message '{Command}': {Error}",
                        attempt + 1, item.MethodName, ex.Message);
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * (attempt + 1)));
                }
            }
            if (!sent)
            {
                _logger.LogError("Failed to send offline message '{Command}' after 3 attempts", item.MethodName);
            }
            dequeued++;
        }

        _logger.LogInformation("Dequeued {DequeueCount} item(s) from cache", dequeued);
    }

    public async Task<HttpStatusCode> InvokeVoidAsync(string methodName, StreamFlowMessage sfMessage) 
    {
        try
        {
            if (Connection?.State is HubConnectionState.Connected && _isRegistered is true && !_isRegistering)
            {
                return await Connection?.InvokeAsync<HttpStatusCode>(methodName, sfMessage);
            }
            
            var maxQueue = StreamFlowConfiguration.QueueDepth > 0 ? StreamFlowConfiguration.QueueDepth : 10_000;
            if (_offlineQueue.Count >= maxQueue)
            {
                _logger.LogError("Offline queue full ({Count}), dropping message '{CommandName}'",
                    _offlineQueue.Count, sfMessage.CommandName);
                return HttpStatusCode.ServiceUnavailable;
            }

            _logger.LogInformation("Invoked Method \'{MethodName}\' is queued, waiting for connection to be re-established", methodName);
            _offlineQueue.Enqueue(new(methodName, sfMessage));
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

        _logger.LogDebug("Invoking Method \'{SfMessageCommandName}\' on {SfMessageRecipientId}", sfMessage.CommandName, sfMessage.RecipientId);

        try
        {
            if (Connection?.State is not HubConnectionState.Connected || !_isRegistered || _isRegistering)
            {
                // Queue for offline delivery
                var maxQueue = StreamFlowConfiguration.QueueDepth > 0 ? StreamFlowConfiguration.QueueDepth : 10_000;
                if (_offlineQueue.Count >= maxQueue)
                {
                    return CreateErrorResponse(sfMessage, HttpStatusCode.ServiceUnavailable, "Offline queue full");
                }
                _offlineQueue.Enqueue(new(nameof(IStreamFlow.Push), sfMessage));
                return new() { ResponseStatusCode = HttpStatusCode.Processing };
            }

            // Use hub's Invoke — hub routes to recipient, waits for response, returns directly
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(StreamFlowConfiguration.RpcTimeoutSeconds));
            var invokeResponse = await Connection.InvokeAsync<StreamFlowInvokeResponse>(
                "Invoke", sfMessage, cts.Token);

            startTimer.Stop();
            _logger.LogDebug("Invoked Method \'{SfMessageCommandName}\' completed in {ResponseTime}ms", sfMessage.CommandName, startTimer.ElapsedMilliseconds);

            return new()
            {
                ResponseStatusCode = invokeResponse.HttpStatusCode,
                Data = invokeResponse.Response,
                Message = invokeResponse.Message,
                Duration = startTimer.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("RPC timeout for method '{SfMessageCommandName}' after {Timeout}s", sfMessage.CommandName, StreamFlowConfiguration.RpcTimeoutSeconds);
            return CreateErrorResponse(sfMessage, HttpStatusCode.RequestTimeout,
                $"RPC timeout for method '{sfMessage.CommandName}' on {sfMessage.RecipientId}");
        }
        catch (Exception e)
        {
            _logger.LogError("Invoke '{SfMessageCommandName}' failed: {EMessage}", sfMessage.CommandName, e.Message);
            return CreateErrorResponse(sfMessage, HttpStatusCode.InternalServerError,
                $"Error invoking method '{sfMessage.CommandName}' on {sfMessage.RecipientId}");
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