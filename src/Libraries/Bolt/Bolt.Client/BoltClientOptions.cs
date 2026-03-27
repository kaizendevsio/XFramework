namespace Bolt.Client;

/// <summary>
/// Configuration for BoltClient connections.
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

    /// <summary>Target throughput for media streams in Kbps. Default: 5000 (5 Mbps).</summary>
    public int TargetThroughputKbps { get; set; } = 5000;

    /// <summary>How long an incoming call rings before auto-rejecting, in seconds. Default: 30.</summary>
    public int CallRingTimeoutSeconds { get; set; } = 30;
}
