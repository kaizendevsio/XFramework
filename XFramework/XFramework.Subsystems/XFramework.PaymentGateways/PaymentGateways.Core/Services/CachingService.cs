using PaymentGateways.Core.Interfaces;
using System.Collections.Generic;
using XFramework.Domain.Generic.BusinessObjects;

namespace PaymentGateways.Core.Services
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