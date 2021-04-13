using System.Collections.Generic;
using XFramework.Core.Interfaces;
using XFramework.Domain.Generic.BusinessObjects;

namespace XFramework.Core.Services
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