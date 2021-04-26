using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using StreamFlow.Domain.BusinessObjects;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Domain.Generic.Contracts.Requests;

namespace StreamFlow.Core.Interfaces
{
    public interface ICachingService
    {
        public List<StreamFlowClientBO> Clients { get; set; }
        public List<StreamFlowClientBO> AbsoluteClients { get; set; }
        public List<StreamFlowMessageBO> QueuedMessages { get; set; }
        public ConcurrentDictionary<Guid, TaskCompletionSource<StreamFlowMessageBO>> PendingMethodCalls { get; set; }
    }
}