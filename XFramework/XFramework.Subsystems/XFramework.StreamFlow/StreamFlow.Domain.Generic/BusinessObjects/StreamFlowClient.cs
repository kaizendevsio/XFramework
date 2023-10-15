using XFramework.Domain.Generic.BusinessObjects;

namespace StreamFlow.Domain.Generic.BusinessObjects;

public class StreamFlowClient : GenericAudit
{
    public string Name { get; set; }
    public string StreamId { get; set; }
    public StreamFlowQueue Queue { get; set; }
    public DateTime ConnectedAt { get; set; }
}