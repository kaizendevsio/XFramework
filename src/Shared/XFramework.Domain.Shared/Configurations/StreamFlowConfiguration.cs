namespace XFramework.Domain.Shared.Configurations;

public class StreamFlowConfiguration
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
    public int RpcTimeoutSeconds { get; set; } = 30;
    public int MaxPendingRpcCalls { get; set; } = 1000;
    public int DeadLetterQueueCapacity { get; set; } = 100_000;
    public int MaxParallelInvocationsPerClient { get; set; } = 64;
}