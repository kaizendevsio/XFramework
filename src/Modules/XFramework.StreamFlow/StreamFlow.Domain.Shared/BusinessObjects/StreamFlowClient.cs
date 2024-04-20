using XFramework.Domain.Shared.BusinessObjects;

namespace StreamFlow.Domain.Shared.BusinessObjects;

public class StreamFlowClient
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string StreamId { get; set; }
    public StreamFlowQueue Queue { get; set; }
    public DateTime ConnectedAt { get; set; }
}