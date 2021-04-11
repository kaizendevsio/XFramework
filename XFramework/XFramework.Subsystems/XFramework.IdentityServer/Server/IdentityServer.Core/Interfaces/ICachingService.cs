using System.Collections.Generic;
using XFramework.Domain.Generic.BusinessObjects;

namespace IdentityServer.Core.Interfaces
{
    public interface ICachingService
    {
        public List<IdentitySessionBO> IdentitySessions { get; set; }
    }
}