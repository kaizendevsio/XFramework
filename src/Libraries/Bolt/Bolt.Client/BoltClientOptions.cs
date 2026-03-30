namespace Bolt.Client;

/// <summary>
/// Configuration for BoltClient connections (RPC, Push, Streaming).
/// For media-specific options, see Bolt.Media.MediaStreamOptions.
/// </summary>
public class BoltClientOptions
{
    /// <summary>RPC call timeout in seconds. Default: 30.</summary>
    public int RpcTimeoutSeconds { get; set; } = 30;

    /// <summary>Minimum WebSocket connections to maintain. Default: 1.</summary>
    public int MinConnections { get; set; } = 1;

    /// <summary>Maximum WebSocket connections to scale to. Default: ProcessorCount.</summary>
    public int MaxConnections { get; set; } = Environment.ProcessorCount;

    /// <summary>Pending send count threshold to trigger connection scale-up. Default: 48.</summary>
    public int ScaleUpThreshold { get; set; } = 48;

    /// <summary>
    /// Payload size threshold (bytes) above which InvokeAsync transparently switches
    /// to BoltStream chunking instead of a single Request frame.
    /// Default: 65536 (64KB). Set to int.MaxValue to disable.
    /// </summary>
    public int LargePayloadThreshold { get; set; } = 65536;

    /// <summary>Chunk size for large payload streaming. Default: 32768 (32KB).</summary>
    public int StreamChunkSize { get; set; } = 32768;
}
