using System.Collections.Generic;
using IdentityServer.Core.Interfaces;
using XFramework.Domain.Generic.BusinessObjects;

namespace IdentityServer.Core.Services
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