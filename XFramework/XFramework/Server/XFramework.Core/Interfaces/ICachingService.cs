using System.Collections.Generic;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Interfaces;

namespace XFramework.Core.Interfaces
{
    public interface ICachingService : IXFrameworkService
    {
        public List<IdentitySessionBO> IdentitySessions { get; set; }
    }
}