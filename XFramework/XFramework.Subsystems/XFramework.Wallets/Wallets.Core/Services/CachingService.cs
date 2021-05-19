using System.Collections.Generic;
using Wallets.Core.Interfaces;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Core.Services
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