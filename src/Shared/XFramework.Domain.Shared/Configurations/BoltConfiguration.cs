namespace XFramework.Domain.Shared.Configurations;

public class BoltConfiguration
{
    public List<Uri>? ServerUrls { get; set; }
    public Guid? ClientGuid { get; set; }
    public string? ClientName { get; set; }
    public string? ClientDescription { get; set; }
    public int ReconnectDelay { get; set; }
    public int MaxRetry { get; set; }
    public string? Signature { get; set; }
    public int QueueDepth { get; set; }
    public bool QueueMessages { get; set; }
    public bool Anonymous { get; set; }
    public string? AccessToken { get; set; }
    public bool SendAccessTokenAsQueryString { get; set; }
    public bool GenerateServiceAccessToken { get; set; } = true;
    public int RpcTimeoutSeconds { get; set; } = 30;
    public int MaxPendingRpcCalls { get; set; } = 1000;
    public int DeadLetterQueueCapacity { get; set; } = 100_000;
    public int MaxParallelInvocationsPerClient { get; set; } = 64;

    // Connection pooling
    public int MinConnections { get; set; } = 1;
    public int MaxConnections { get; set; } = Environment.ProcessorCount;
    public int ScaleUpThreshold { get; set; } = 48;
    public int IdleTimeoutSeconds { get; set; } = 30;
}
