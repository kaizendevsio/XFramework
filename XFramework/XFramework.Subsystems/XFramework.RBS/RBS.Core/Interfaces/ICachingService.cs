using System.Collections.Generic;
using RBS.Domain.BusinessObjects;

namespace RBS.Core.Interfaces
{
    public interface ICachingService
    {
        public List<IdentitySessionBO> IdentitySessions { get; set; }
        public bool IsStarted { get; set; }
        public decimal Wala { get; set; }
        public decimal Meron { get; set; }
    }
}