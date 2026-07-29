using Bolt.Protocol;
using Bolt.Protocol.Transport;

namespace Bolt.Client;

/// <summary>
/// Configuration for BoltClient connections (RPC, Push, Streaming).
/// For media-specific options, see Bolt.Media.MediaStreamOptions.
/// </summary>
public class BoltClientOptions
{
    internal BoltClientOptions Clone()
    {
        var clone = (BoltClientOptions)MemberwiseClone();
        clone.PreferredTransports = [.. PreferredTransports];
        return clone;
    }

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

    /// <summary>Maximum logical request or response body accepted through large-RPC streaming. Default: 32 MiB.</summary>
    public int MaxLargeRpcPayloadBytes { get; set; } = 32 * 1024 * 1024;

    /// <summary>Maximum aggregate bytes reserved by concurrent large-RPC reassembly. Default: 64 MiB.</summary>
    public long MaxBufferedLargeRpcBytes { get; set; } = 64L * 1024 * 1024;

    /// <summary>Per-connection receive fragment buffer. Default: 65536 bytes.</summary>
    public int ReceiveBufferBytes { get; set; } = 64 * 1024;

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

    /// <summary>Combines already queued non-media frames into Bolt wire-v2 batches. Default: enabled.</summary>
    public bool EnableBatching { get; set; } = true;

    /// <summary>Maximum time to wait when the send queue is full. Default: RPC timeout.</summary>
    public int SendEnqueueTimeoutMs { get; set; } = 0;

    /// <summary>
    /// Maximum buffered pub/sub events per subscription. Slow transient subscriptions drop
    /// the oldest unread event; slow durable subscriptions fail locally without dropping
    /// unread entries so persisted messages can replay later.
    /// Default: 4096.
    /// </summary>
    public int PubSubChannelCapacity { get; set; } = 4096;

    /// <summary>Maximum unread chunks buffered by each Bolt stream. Default: 1024.</summary>
    public int StreamInboundCapacity { get; set; } = 1024;

    /// <summary>Maximum concurrently executing inbound request and push handlers. Default: 128.</summary>
    public int MaxConcurrentInboundHandlers { get; set; } = 128;

    /// <summary>Maximum active inbound and outbound logical streams. Default: 1024.</summary>
    public int MaxActiveStreams { get; set; } = 1024;

    /// <summary>
    /// Payload size threshold (bytes) above which InvokeAsync transparently switches
    /// to BoltStream chunking instead of a single Request/Response frame.
    /// Default: 2097152 (2 MiB). Single frames work fine up to several MB via
    /// WebSocket fragmentation. Streaming helps for very large transfers where
    /// holding the entire payload in memory is undesirable.
    /// Values above the safe frame payload ceiling are clamped to that ceiling.
    /// </summary>
    public int LargePayloadThreshold { get; set; } = 2 * 1024 * 1024;

    /// <summary>
    /// Chunk size for large payload streaming. Default: 131051 bytes, which keeps the
    /// complete StreamData frame within a 128 KiB buffer. Other values remain configurable.
    /// </summary>
    public int StreamChunkSize { get; set; } = (128 * 1024) - BoltCodec.StreamDataHeaderSize;

    /// <summary>
    /// Runs RPC continuations away from the receive loop so response handling cannot
    /// stall the physical receive path under concurrent load.
    /// </summary>
    public bool RunRpcContinuationsAsynchronously { get; set; } = true;

    /// <summary>Maximum payload bytes awaiting physical-send completion per large-RPC transfer. Default: 2 MiB.</summary>
    public int MaxLargeRpcPipelineBytes { get; set; } = 2 * 1024 * 1024;
}
