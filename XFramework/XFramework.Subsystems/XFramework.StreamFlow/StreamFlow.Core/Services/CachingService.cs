using StreamFlow.Core.Interfaces;
using System.Collections.Generic;
using StreamFlow.Domain.BusinessObjects;
using StreamFlow.Domain.Generic.BusinessObjects;

namespace StreamFlow.Core.Services
{
    public class CachingService : ICachingService
    {
        public CachingService()
        {
        }

        public List<StreamFlowClientBO> Clients { get; set; } = new();
        public List<StreamFlowClientBO> AbsoluteClients { get; set; } = new();
        public List<StreamFlowMessageBO> QueuedMessages { get; set; } = new();
    }
}