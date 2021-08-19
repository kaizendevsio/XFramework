using RBS.Core.Interfaces;
using System.Collections.Generic;
using RBS.Domain.BusinessObjects;

namespace RBS.Core.Services
{
    public class CachingService : ICachingService
    {
        public CachingService()
        {
            IdentitySessions = new List<IdentitySessionBO>();
        }

        public List<IdentitySessionBO> IdentitySessions { get; set; }
        
        public bool IsStarted { get; set; }
        public decimal Wala { get; set; }
        public decimal Meron { get; set; }
    }
}