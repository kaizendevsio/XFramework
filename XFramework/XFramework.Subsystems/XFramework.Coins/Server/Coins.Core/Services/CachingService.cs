using Coins.Core.Interfaces;
using System.Collections.Generic;
using Coins.Domain.BusinessObjects;
using XFramework.Domain.Generic.BusinessObjects;

namespace Coins.Core.Services
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