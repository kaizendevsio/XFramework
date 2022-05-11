using System;
using System.Collections.Concurrent;
using StreamFlow.Core.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;
using StreamFlow.Domain.BusinessObjects;
using StreamFlow.Domain.Generic.BusinessObjects;
using StreamFlow.Domain.Generic.Contracts.Requests;

namespace StreamFlow.Core.Services
{
    public class CachingService : ICachingService
    {
        public CachingService()
        {
        }

        public List<StreamFlowClientBO> LatestClients { get; set; } = new();
        public List<StreamFlowClientBO> Clients { get; set; } = new();
        public List<StreamFlowClientBO> AbsoluteClients { get; set; } = new();
        public List<StreamFlowMessageBO> QueuedMessages { get; set; } = new();
        public ConcurrentDictionary<Guid, TaskCompletionSource<StreamFlowMessageBO>> PendingMethodCalls { get; set; } = new();
    }
}