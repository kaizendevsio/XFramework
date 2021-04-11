using System.Collections.Generic;
using Records.Domain.BusinessObjects;
using XFramework.Domain.Generic.BusinessObjects;

namespace Records.Core.Interfaces
{
    public interface ICachingService
    {
        public List<IdentitySessionBO> IdentitySessions { get; set; }
    }
}