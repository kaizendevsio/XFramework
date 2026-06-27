using Bolt.Protocol.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bolt.Client;

public class BoltClientBuilder
{
    internal Uri ServerUri { get; private set; } = null!;
    internal string ClientId { get; private set; } = Guid.NewGuid().ToString("N");
    internal string ClientName { get; private set; } = "BoltClient";
    internal BoltClientOptions Options { get; } = new();
    internal readonly List<(string Command, Func<ReadOnlyMemory<byte>, Guid, Task<(System.Net.HttpStatusCode, ReadOnlyMemory<byte>)>> Handler)> Handlers = [];
    internal readonly List<(string Command, Func<BoltStream, Task> Handler)> StreamHandlers = [];
    internal bool AutoConnect { get; private set; } = true;

    /// <summary>Set the Bolt server URI. Required.</summary>
    public BoltClientBuilder WithServer(string uri) { ServerUri = new Uri(uri); return this; }

    /// <summary>Set the Bolt server URI. Required.</summary>
    public BoltClientBuilder WithServer(Uri uri) { ServerUri = uri; return this; }

    /// <summary>Set the client identity used for registration and routing.</summary>
    public BoltClientBuilder WithClientId(string clientId) { ClientId = clientId; return this; }

    /// <summary>Set a friendly display name for this client.</summary>
    public BoltClientBuilder WithClientName(string name) { ClientName = name; return this; }

    /// <summary>Configure connection options.</summary>
    public BoltClientBuilder WithOptions(Action<BoltClientOptions> configure) { configure(Options); return this; }

    /// <summary>Set the minimum number of connections. Default: 1.</summary>
    public BoltClientBuilder WithMinConnections(int min) { Options.MinConnections = min; return this; }

    /// <summary>Set the maximum number of connections. Default: ProcessorCount.</summary>
    public BoltClientBuilder WithMaxConnections(int max) { Options.MaxConnections = max; return this; }

    /// <summary>
    /// Configure preferred transports. Default: QUIC, WebTransport, WebSocket.
    /// Example: bolt.WithTransports(BoltTransport.WebSocket) to force WebSocket only.
    /// </summary>
    public BoltClientBuilder WithTransports(params BoltTransport[] transports)
    {
        Options.PreferredTransports = transports;
        return this;
    }

    /// <summary>Set the timeout per transport attempt in milliseconds. Default: 3000.</summary>
    public BoltClientBuilder WithTransportTimeout(int ms)
    {
        Options.TransportAttemptTimeoutMs = ms;
        return this;
    }

    /// <summary>Set the RPC timeout in seconds. Default: 30.</summary>
    public BoltClientBuilder WithTimeout(int seconds) { Options.RpcTimeoutSeconds = seconds; return this; }

    /// <summary>Use a static bearer token for Bolt handshakes.</summary>
    public BoltClientBuilder WithAccessToken(string? accessToken)
    {
        Options.AccessToken = accessToken;
        return this;
    }

    /// <summary>Use a per-connection bearer token provider for Bolt handshakes.</summary>
    public BoltClientBuilder WithAccessTokenProvider(Func<CancellationToken, ValueTask<string?>> provider)
    {
        Options.AccessTokenProvider = provider;
        return this;
    }

    /// <summary>Send the bearer token as ?access_token= for browser WebSocket clients.</summary>
    public BoltClientBuilder UseAccessTokenQueryString(bool enabled = true)
    {
        Options.SendAccessTokenAsQueryString = enabled;
        return this;
    }

    /// <summary>Register an RPC handler for incoming requests.</summary>
    public BoltClientBuilder HandleRpc(string commandName,
        Func<ReadOnlyMemory<byte>, Guid, Task<(System.Net.HttpStatusCode, ReadOnlyMemory<byte>)>> handler)
    {
        Handlers.Add((commandName, handler));
        return this;
    }

    /// <summary>Register a stream handler for incoming streams.</summary>
    public BoltClientBuilder HandleStream(string commandName, Func<BoltStream, Task> handler)
    {
        StreamHandlers.Add((commandName, handler));
        return this;
    }

    /// <summary>
    /// Disable auto-connect on startup. You'll need to call ConnectAsync() manually.
    /// Default: auto-connects as a hosted service.
    /// </summary>
    public BoltClientBuilder DisableAutoConnect() { AutoConnect = false; return this; }
}

public static class BoltClientExtensions
{
    /// <summary>
    /// Add a Bolt client to the service collection with fluent configuration.
    ///
    /// Usage:
    ///   builder.Services.AddBoltClient(bolt => bolt
    ///       .WithServer("ws://localhost:5000/bolt")
    ///       .WithClientId("my-service")
    ///       .WithClientName("MyService")
    ///       .WithMinConnections(2)
    ///       .WithTimeout(30)
    ///       .HandleRpc("hello", async (payload, id) => { ... })
    ///   );
    /// </summary>
    public static IServiceCollection AddBoltClient(this IServiceCollection services, Action<BoltClientBuilder> configure)
    {
        var builder = new BoltClientBuilder();
        configure(builder);

        if (builder.ServerUri is null)
            throw new ArgumentException("Bolt server URI is required. Call .WithServer(\"ws://...\")");

        // Register BoltClient as singleton — created via factory
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<BoltClient>();
            var client = new BoltClient(builder.ServerUri, builder.ClientId, builder.ClientName, builder.Options, logger);

            foreach (var (cmd, handler) in builder.Handlers)
                client.RegisterHandler(cmd, handler);

            foreach (var (cmd, handler) in builder.StreamHandlers)
                client.RegisterStreamHandler(cmd, handler);

            return client;
        });

        // Auto-connect via hosted service if enabled
        if (builder.AutoConnect)
            services.AddHostedService<BoltClientHostedService>();

        return services;
    }
}

/// <summary>
/// Hosted service that connects the BoltClient on application startup
/// and disconnects on shutdown.
/// </summary>
internal class BoltClientHostedService : IHostedService
{
    private readonly BoltClient _client;
    private readonly ILogger<BoltClientHostedService> _logger;

    public BoltClientHostedService(BoltClient client, ILogger<BoltClientHostedService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _logger.LogInformation("Bolt client connecting to server...");
        await _client.ConnectWithRetryAsync(ct);
        _logger.LogInformation("Bolt client connected: {ClientId}", _client.IsConnected);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await _client.DisposeAsync();
    }
}
