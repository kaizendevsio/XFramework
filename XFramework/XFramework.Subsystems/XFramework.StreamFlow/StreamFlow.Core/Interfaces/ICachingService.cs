using System.Collections.Generic;
using StreamFlow.Domain.BusinessObjects;

namespace StreamFlow.Core.Interfaces
{
    public interface ICachingService
    {
        public List<IdentitySessionBO> IdentitySessions { get; set; }
    }
}