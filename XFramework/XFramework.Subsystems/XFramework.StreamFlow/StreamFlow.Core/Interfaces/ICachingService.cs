using System.Collections.Generic;
using StreamFlow.Domain.BusinessObjects;
using StreamFlow.Domain.Generic.BusinessObjects;

namespace StreamFlow.Core.Interfaces
{
    public interface ICachingService
    {
        public List<StreamFlowClientBO> Clients { get; set; }
        public List<StreamFlowClientBO> AbsoluteClients { get; set; }
        public List<StreamFlowMessageBO> QueuedMessages { get; set; }
    }
}