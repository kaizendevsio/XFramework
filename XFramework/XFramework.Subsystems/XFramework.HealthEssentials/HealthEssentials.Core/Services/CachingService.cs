namespace HealthEssentials.Core.Services;

public class CachingService : ICachingService
{
    public CachingService()
    {
        IdentitySessions = new List<IdentitySessionBO>();
    }

    public List<IdentitySessionBO> IdentitySessions { get; set; }
}