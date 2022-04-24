using XFramework.Domain.Generic.Interfaces;

namespace Messaging.Core.Interfaces;

public interface ICachingService : IXFrameworkService
{
    public List<IdentitySessionBO> IdentitySessions { get; set; }
}