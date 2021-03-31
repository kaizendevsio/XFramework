using System;
using XFramework.Domain.Generic.BusinessObjects;

namespace StreamFlow.Domain.BusinessObjects
{
    public class StreamFlowClientBO : GenericAuditBO
    {
        public string Name { get; set; }
        public string StreamId { get; set; }
        public StreamFlowQueueBO Queue { get; set; }
        public DateTime ConnectedAt { get; set; }
    }
}