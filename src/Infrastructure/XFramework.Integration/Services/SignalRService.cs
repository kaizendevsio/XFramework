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
using Bolt.Domain.Shared.Abstractions;
using Bolt.Domain.Shared.BusinessObjects;
using Bolt.Domain.Shared.Contracts.Requests;
using Bolt.Domain.Shared.Contracts.Responses;
using Bolt.Domain.Shared.Enums;
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

    private readonly ConcurrentQueue<(string MethodName, BoltMessage BoltMessage)> _offlineQueue = new();
    protected TaskCompletionSource TaskCompletionSource { get; set; } = new();

    // Connection pool — replaces the single Connection property
    private ConnectionPool? _connectionPool;

    // Primary connection (backward compat for event handlers, handler registration)
    public HubConnection? Connection { get; set; }

    // Legacy single-connection pending calls (for InvokeResponseHandler pattern)
    public ConcurrentDictionary<Guid, PooledRpcCall> PendingMethodCalls { get; set; } = new();

    public BoltConfiguration BoltConfiguration { get; set; } = new();

    public SignalRService(IHostEnvironment hostEnvironment, IConfiguration configuration, ILogger<SignalRService> logger, ILogger<BaseSignalRHandler> baseLogger, IServiceScopeFactory scopeFactory)
    {
        _hostEnvironment = hostEnvironment;
        _configuration = configuration;
        _baseLogger = baseLogger;
        _scopeFactory = scopeFactory;
        _logger = logger;
        configuration.Bind(nameof(BoltConfiguration), BoltConfiguration);

        InitializeService();
    }

    private Uri? ResolveServerUrl()
    {
        var envConfig = _configuration["BOLT_SERVER_URLS"];

        if ((BoltConfiguration.ServerUrls is null || !BoltConfiguration.ServerUrls.Any()) && string.IsNullOrEmpty(envConfig))
            return null;

        return !string.IsNullOrEmpty(envConfig)
            ? new Uri(envConfig)
            : BoltConfiguration?.ServerUrls?.FirstOrDefault();
    }

    private HubConnection BuildConnection(Uri serverUrl)
    {
        return new HubConnectionBuilder()
            .WithUrl(serverUrl, (opts) =>
            {
                if (OperatingSystem.IsBrowser()) return;

                if (serverUrl.AbsoluteUri.StartsWith("https://localhost", StringComparison.OrdinalIgnoreCase)
                    || serverUrl.AbsoluteUri.StartsWith("https://127.0.0.1", StringComparison.OrdinalIgnoreCase))
                {
                    opts.HttpMessageHandlerFactory = (message) =>
                    {
                        if (message is HttpClientHandler clientHandler)
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
    }

    private void InitializeService()
    {
        var serverUrl = ResolveServerUrl();
        if (serverUrl is null)
        {
            _logger.LogWarning("Bolt configuration is not set, therefore SignalR client service is disabled");
            return;
        }

        // Build primary connection
        Connection = BuildConnection(serverUrl);

        // Initialize connection pool
        _connectionPool = new ConnectionPool(
            connectionFactory: () => BuildConnection(serverUrl),
            onConnectionReady: RegisterPooledConnectionAsync,
            BoltConfiguration,
            _logger);
        _connectionPool.AddPrimary(Connection);

        HandleEvents();
        Task.Run(async () => await EnsureConnection());
    }

    /// <summary>
    /// Register a newly scaled-up connection with the Bolt hub.
    /// </summary>
    private async Task RegisterPooledConnectionAsync(HubConnection connection)
    {
        var serviceName = !string.IsNullOrEmpty(BoltConfiguration.ClientName)
            ? BoltConfiguration.ClientName.Split(".").First()
            : Assembly.GetEntryAssembly()!.GetName().Name!.Split(".").First()
              ?? throw new ArgumentException("Assembly name is not set");
        var serviceId = serviceName.ToSha256();
        var clientId = BoltConfiguration.Anonymous ? $"sfc_{Guid.NewGuid()}" : serviceId ?? throw new ArgumentException("Bolt client Id is not set");

        var request = new BoltHubClient()
        {
            Id = clientId,
            Name = BoltConfiguration.ClientName,
            Queue = new BoltQueue()
        };
        await connection.InvokeAsync<HttpStatusCode>(nameof(IBoltTransport.Register), request);

        // Register handlers on the new connection
        RegisterHandlersOnConnection(connection);
        RegisterInvokeResponseOnConnection(connection);

        _logger.LogInformation("Pooled connection registered with Bolt hub");
    }

    /// <summary>
    /// Register the InvokeResponse handler on a specific connection.
    /// </summary>
    private void RegisterInvokeResponseOnConnection(HubConnection connection)
    {
        connection.On<BoltMessage>(nameof(IBoltTransport.InvokeResponseHandler),
            (response) =>
            {
                try
                {
                    // Check all pooled connections' pending calls
                    if (_connectionPool is not null)
                    {
                        foreach (var pooledConn in _connectionPool.GetAll())
                        {
                            if (pooledConn.PendingCalls.TryRemove(response.RequestId, out var pooledRpc))
                            {
                                pooledRpc.SetResult(response);
                                return;
                            }
                        }
                    }

                    // Fallback: check legacy pending calls
                    if (PendingMethodCalls.TryRemove(response.RequestId, out var rpcCall))
                    {
                        rpcCall.SetResult(response);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogInformation("[{Caller}] Processing response for '{Request}' resulted in exception: {Error}", nameof(HandleInvokeResponseEvent), response.CommandName, e.Message);
                }
            });
    }

    /// <summary>
    /// Register generated ISignalREventHandler instances on a specific connection.
    /// </summary>
    private void RegisterHandlersOnConnection(HubConnection connection)
    {
        var handlers = Assembly.GetEntryAssembly()?.ExportedTypes
            .Where(x => typeof(ISignalREventHandler).IsAssignableFrom(x) && x is { IsInterface: false, IsAbstract: false })
            .Select(Activator.CreateInstance)
            .Cast<ISignalREventHandler>()
            .ToList();

        handlers?.ForEach(handler => handler.Handle(connection, _baseLogger, _scopeFactory));
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
        var typesImplementingBoltRequest = Assembly.GetAssembly(typeof(T)).GetTypes()
            .Where(x => !x.IsInterface && !x.IsAbstract)
            .SelectMany(x => x.GetInterfaces(), (x, i) => new { Type = x, Interface = i })
            .Where(x => x.Interface.IsGenericType && x.Interface.GetGenericTypeDefinition() == typeof(IBoltRequest<,>))
            .ToList();

        foreach (var type in typesImplementingBoltRequest)
        {
            var genericArguments = type.Interface.GetGenericArguments();

            if (genericArguments.Length == 2)
            {
                Type tRequest = genericArguments[0];
                Type tResponse = genericArguments[1];

                Console.WriteLine($"Type: {type.Type.Name}, TRequest: {tRequest.Name}, TResponse: {tResponse.Name}");

                if (tResponse.IsAssignableTo(typeof(ICmdWithResultResponse)))
                {
                    var methodInfo = GetType()
                        .GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
                        .First(m => m.Name == nameof(HandleRequestCmd) && m.GetGenericArguments().Length == 2);

                    var genericMethod = methodInfo.MakeGenericMethod(tRequest, tResponse.GetGenericArguments().First());
                    genericMethod.Invoke(this, [Connection, _baseLogger, _scopeFactory]);
                }
                else if (tResponse.IsAssignableTo(typeof(ICmdResponse)))
                {
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
        RegisterHandlersOnConnection(Connection!);
    }

    public async Task StartEventListener(string topic)
    {
        if (string.IsNullOrEmpty(topic)) return;
        var client = new BoltHubClient
        {
            Queue = new()
            {
                Name = topic
            },
        };
        var r = await Connection?.InvokeAsync<HttpStatusCode>(nameof(IBoltTransport.Subscribe), client);
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
        RegisterInvokeResponseOnConnection(Connection!);
    }

    private void HandleClosedEvent()
    {
        Connection!.Closed += async connectionId =>
        {
            _logger.LogInformation("Connection to Bolt server closed, connectionId: {ConnectionId}", connectionId);

            // Cancel all pending RPCs across all pool connections
            if (_connectionPool is not null)
            {
                foreach (var pooledConn in _connectionPool.GetAll())
                {
                    foreach (var (id, _) in pooledConn.PendingCalls)
                    {
                        if (pooledConn.PendingCalls.TryRemove(id, out var rpcCall))
                            rpcCall.SetException(new InvalidOperationException("Connection lost"));
                    }
                }
            }

            // Cancel legacy pending RPCs
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
        Connection?.On<string, string>(nameof(IBoltTransport.TelemetryCall), (data, message) => { _logger.LogInformation("Telemetry Call ({Now}): {Message}", DateTime.Now, message); });
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
            _logger.LogInformation("Connection to Bolt server restored");
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
            Debug.Assert(Connection?.State == HubConnectionState.Reconnecting);
            _isRegistered = false;
            _isRegistering = false;

            _logger.LogInformation("Connection to Bolt server lost, trying to reconnect..");

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
                if (Connection?.State is not HubConnectionState.Disconnected && _isRegistered)
                    return true;

                if (Connection?.State == HubConnectionState.Reconnecting)
                {
                    _logger.LogInformation("Connection is in the process of reconnecting, waiting...");
                    return true;
                }

                if (_isRegistering)
                {
                    _logger.LogInformation("Request Postponed, Awaiting Registration..");
                    return true;
                }

                if (Connection?.State == HubConnectionState.Disconnected)
                {
                    var startTimer = Stopwatch.StartNew();
                    _logger.LogInformation("Connecting to Bolt server..");

                    await Connection.StartAsync();
                    startTimer.Stop();
                    _logger.LogInformation("Connecting to Bolt server.. Done in {ResponseTime}ms", startTimer.ElapsedMilliseconds);
                }

                if (Connection?.State == HubConnectionState.Connected)
                {
                    await RegisterConnection();
                }

                return true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to connect to Bolt server: {EMessage} : {InnerExceptionMessage}", e.Message, e.InnerException?.Message);
                _logger.LogInformation("Retrying in {ReconnectDelay}ms", BoltConfiguration.ReconnectDelay);
                await Task.Delay(BoltConfiguration.ReconnectDelay);
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

        var serviceName = !string.IsNullOrEmpty(BoltConfiguration.ClientName)
            ? BoltConfiguration.ClientName.Split(".").First()
            : Assembly.GetEntryAssembly()!.GetName().Name!.Split(".").First()
              ?? throw new ArgumentException("Assembly name is not set");
        var serviceId = serviceName.ToSha256();

        _clientId = BoltConfiguration.Anonymous ? $"sfc_{Guid.NewGuid()}" : serviceId ?? throw new ArgumentException("Bolt client Id is not set");
        _logger.LogInformation("Registering bolt client with id {ClientId}", _clientId);

        var request = new BoltHubClient()
        {
            Id = _clientId,
            Name = BoltConfiguration.ClientName,
            Queue = new BoltQueue()
        };
        await Connection!.InvokeAsync<HttpStatusCode>(nameof(IBoltTransport.Register), request);

        startTimer.Stop();
        _logger.LogInformation("Registering Connection.. Done in {ResponseTime}ms", startTimer.ElapsedMilliseconds);

        // Mark primary pooled connection as registered
        var primaryPooled = _connectionPool?.GetAll().FirstOrDefault();
        if (primaryPooled is not null) primaryPooled.IsRegistered = true;

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
                    await Connection.InvokeAsync<HttpStatusCode>(item.MethodName, item.BoltMessage);
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

    public async Task<HttpStatusCode> InvokeVoidAsync(string methodName, BoltMessage sfMessage)
    {
        try
        {
            if (Connection?.State is HubConnectionState.Connected && _isRegistered is true && !_isRegistering)
            {
                return await Connection?.InvokeAsync<HttpStatusCode>(methodName, sfMessage);
            }

            var maxQueue = BoltConfiguration.QueueDepth > 0 ? BoltConfiguration.QueueDepth : 10_000;
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

    public async Task<BoltRpcResult> InvokeAsync(BoltMessage sfMessage)
    {
        var startTimer = Stopwatch.StartNew();
        sfMessage.ClientId = _clientId;

        _logger.LogDebug("Invoking Method \'{SfMessageCommandName}\' on {SfMessageRecipientId}", sfMessage.CommandName, sfMessage.RecipientId);

        try
        {
            // Get the best connection from the pool
            var pooledConn = _connectionPool?.GetConnection();
            var connection = pooledConn?.Connection ?? Connection;

            if (connection?.State is not HubConnectionState.Connected || !_isRegistered || _isRegistering)
            {
                var maxQueue = BoltConfiguration.QueueDepth > 0 ? BoltConfiguration.QueueDepth : 10_000;
                if (_offlineQueue.Count >= maxQueue)
                {
                    return new() { StatusCode = HttpStatusCode.ServiceUnavailable, Message = "Offline queue full" };
                }
                _offlineQueue.Enqueue(new(nameof(IBoltTransport.Push), sfMessage));
                return new() { StatusCode = HttpStatusCode.Processing };
            }

            pooledConn?.Touch();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(BoltConfiguration.RpcTimeoutSeconds));
            var invokeResponse = await connection.InvokeAsync<BoltInvokeResponse>(
                "Invoke", sfMessage, cts.Token);

            startTimer.Stop();
            _logger.LogDebug("Invoked Method \'{SfMessageCommandName}\' completed in {ResponseTime}ms", sfMessage.CommandName, startTimer.ElapsedMilliseconds);

            // Return stack-allocated struct — no heap allocation for the response wrapper
            return new()
            {
                StatusCode = invokeResponse.HttpStatusCode,
                Data = invokeResponse.Response,
                Message = invokeResponse.Message,
                Duration = startTimer.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("RPC timeout for method '{SfMessageCommandName}' after {Timeout}s", sfMessage.CommandName, BoltConfiguration.RpcTimeoutSeconds);
            return new()
            {
                StatusCode = HttpStatusCode.RequestTimeout,
                Message = $"RPC timeout for method '{sfMessage.CommandName}' on {sfMessage.RecipientId}"
            };
        }
        catch (Exception e)
        {
            _logger.LogError("Invoke '{SfMessageCommandName}' failed: {EMessage}", sfMessage.CommandName, e.Message);
            return new()
            {
                StatusCode = HttpStatusCode.InternalServerError,
                Message = $"Error invoking method '{sfMessage.CommandName}' on {sfMessage.RecipientId}"
            };
        }
    }

}
