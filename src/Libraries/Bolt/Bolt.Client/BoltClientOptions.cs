using Bolt.Protocol;
using Bolt.Protocol.Transport;

namespace Bolt.Client;

/// <summary>
/// Configuration for BoltClient connections (RPC, Push, Streaming).
/// For media-specific options, see Bolt.Media.MediaStreamOptions.
/// </summary>
public class BoltClientOptions
{
    /// <summary>
    /// Preferred transport order. The negotiator tries each in sequence, using the first
    /// that succeeds. Transports unavailable on the current platform are auto-skipped.
    /// Default: WebSocket (QUIC is only used for media datagrams, not RPC).
    /// </summary>
    public BoltTransport[] PreferredTransports { get; set; } = [BoltTransport.WebSocket];

    /// <summary>Timeout per transport attempt before trying the next one. Default: 3000ms.</summary>
    public int TransportAttemptTimeoutMs { get; set; } = 3000;

    /// <summary>RPC call timeout in seconds. Default: 30.</summary>
    public int RpcTimeoutSeconds { get; set; } = 30;

    /// <summary>Maximum complete Bolt frame size accepted by receive loops.</summary>
    public int MaxFrameBytes { get; set; } = BoltCodec.DefaultMaxFrameBytes;

    /// <summary>
    /// Static bearer token sent during the Bolt WebSocket handshake.
    /// Prefer AccessTokenProvider for long-running clients.
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// Optional per-connection bearer token provider. Used before AccessToken when configured.
    /// </summary>
    public Func<CancellationToken, ValueTask<string?>>? AccessTokenProvider { get; set; }

    /// <summary>
    /// Sends the access token as ?access_token= instead of an Authorization header.
    /// This is required by browser WebSocket clients that cannot set custom headers.
    /// </summary>
    public bool SendAccessTokenAsQueryString { get; set; }

    /// <summary>Minimum WebSocket connections to maintain. Default: 1.</summary>
    public int MinConnections { get; set; } = 1;

    /// <summary>Maximum WebSocket connections to scale to. Default: ProcessorCount.</summary>
    public int MaxConnections { get; set; } = Environment.ProcessorCount;

    /// <summary>Pending send count threshold to trigger connection scale-up. Default: 48.</summary>
    public int ScaleUpThreshold { get; set; } = 48;

    /// <summary>Bounded send queue capacity per connection. Default: 4096.</summary>
    public int SendQueueCapacity { get; set; } = 4096;

    /// <summary>Maximum time to wait when the send queue is full. Default: RPC timeout.</summary>
    public int SendEnqueueTimeoutMs { get; set; } = 0;

    /// <summary>
    /// Maximum buffered pub/sub events per subscription. Slow transient subscriptions drop
    /// the oldest unread event; slow durable subscriptions fail locally without dropping
    /// unread entries so persisted messages can replay later.
    /// Default: 4096.
    /// </summary>
    public int PubSubChannelCapacity { get; set; } = 4096;

    /// <summary>
    /// Payload size threshold (bytes) above which InvokeAsync transparently switches
    /// to BoltStream chunking instead of a single Request/Response frame.
    /// Default: 10485760 (10MB). Single frames work fine up to several MB via
    /// WebSocket fragmentation. Streaming helps for very large transfers where
    /// holding the entire payload in memory is undesirable.
    /// Set to int.MaxValue to disable auto-streaming.
    /// </summary>
    public int LargePayloadThreshold { get; set; } = 1024 * 1024;

    /// <summary>Chunk size for large payload streaming. Default: 65536 (64KB).</summary>
    public int StreamChunkSize { get; set; } = 65536;
}
