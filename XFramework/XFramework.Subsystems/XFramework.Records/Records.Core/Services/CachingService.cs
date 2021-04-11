using Records.Core.Interfaces;
using System.Collections.Generic;
using Records.Domain.BusinessObjects;
using XFramework.Domain.Generic.BusinessObjects;

namespace Records.Core.Services
{
    public class CachingService : ICachingService
    {
        public CachingService()
        {
            IdentitySessions = new List<IdentitySessionBO>();
        }

        public List<IdentitySessionBO> IdentitySessions { get; set; }
    }
}