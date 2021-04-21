using System.Collections.Generic;
using Coins.Domain.BusinessObjects;
using XFramework.Domain.Generic.BusinessObjects;

namespace Coins.Core.Interfaces
{
    public interface ICachingService
    {
        public List<IdentitySessionBO> IdentitySessions { get; set; }
    }
}