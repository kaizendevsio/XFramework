using System.Collections.Generic;
using IdentityServer.Core.Interfaces;
using IdentityServer.Domain.BusinessObject;

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