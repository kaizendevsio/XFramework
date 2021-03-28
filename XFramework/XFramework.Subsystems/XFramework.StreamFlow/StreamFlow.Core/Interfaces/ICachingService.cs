using IdentityServer.Domain.BusinessObjects;
using System.Collections.Generic;

namespace StreamFlow.Core.Interfaces
{
    public interface ICachingService
    {
        public List<IdentitySessionBO> IdentitySessions { get; set; }
    }
}