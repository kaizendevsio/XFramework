using MessagePack;
using XFramework.Domain.Shared.BusinessObjects;

namespace StreamFlow.Domain.Shared.BusinessObjects;

[MessagePackObject]
public class StreamFlowQueue
{
    [Key(0)]
    public string Name { get; set; }
    
    [Key(1)]
    public Guid Id { get; set; }
}