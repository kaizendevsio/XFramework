using System.Collections.Generic;
using IdentityServer.Domain.BusinessObjects;

namespace IdentityServer.Core.Interfaces
{
    public interface ICachingService
    {
        public List<IdentitySessionBO> IdentitySessions { get; set; }
    }
}