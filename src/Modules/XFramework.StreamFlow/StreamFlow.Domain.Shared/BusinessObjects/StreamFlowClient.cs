using MessagePack;
using XFramework.Domain.Shared.BusinessObjects;

namespace StreamFlow.Domain.Shared.BusinessObjects;

[MessagePackObject]
public class StreamFlowClient
{
    [Key(0)]
    public string Id { get; set; }
    
    [Key(1)]
    public string Name { get; set; }
    
    [Key(2)]
    public string StreamId { get; set; }
    
    [Key(3)]
    public StreamFlowQueue Queue { get; set; }
    
    [Key(4)]
    public DateTime ConnectedAt { get; set; }

    [Key(5)]
    public DateTime LastSeenAt { get; set; }

    [IgnoreMember] public int SuccessCount;
    [IgnoreMember] public int FailureCount;
    [IgnoreMember] public DateTime LastFailureAt;
    [IgnoreMember] public bool IsCircuitOpen;
}