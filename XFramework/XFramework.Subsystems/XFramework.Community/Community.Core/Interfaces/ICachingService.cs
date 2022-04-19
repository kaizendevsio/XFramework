using XFramework.Domain.Generic.Interfaces;

namespace Community.Core.Interfaces;

public interface ICachingService : IXFrameworkService
{
    public List<IdentitySessionBO> IdentitySessions { get; set; }
}