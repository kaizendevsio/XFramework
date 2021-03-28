using IdentityServer.Domain.BusinessObjects;
using System.Collections.Generic;

namespace Records.Core.Interfaces
{
    public interface ICachingService
    {
        public List<IdentitySessionBO> IdentitySessions { get; set; }
    }
}