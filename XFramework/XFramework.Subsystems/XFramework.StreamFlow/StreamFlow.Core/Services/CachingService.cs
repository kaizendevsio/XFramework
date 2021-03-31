using StreamFlow.Core.Interfaces;
using System.Collections.Generic;
using StreamFlow.Domain.BusinessObjects;

namespace StreamFlow.Core.Services
{
    public class CachingService : ICachingService
    {
        public CachingService()
        {
            Clients = new();
        }

        public List<StreamFlowClientBO> Clients { get; set; }
    }
}