using XFramework.Domain.Generic.Interfaces;

namespace IdentityServer.Core.Interfaces;

public interface ICachingService : IXFrameworkService
{
    public List<IdentitySessionBO> IdentitySessions { get; set; }
}