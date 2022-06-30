using System.Collections.Generic;

namespace PaymentGateways.Core.Interfaces
{
    public interface ICachingService : IXFrameworkService
    {
        public List<IdentitySessionBO> IdentitySessions { get; set; }
    }
}