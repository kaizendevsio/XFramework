using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace IdentityServer.Core.Services;

public class CachingService : ICachingService
{
    public CachingService()
    {
        IdentitySessions = new List<IdentitySessionBO>();
        AddressCountryResponseList = new List<AddressCountryResponse>();
    }

    public List<IdentitySessionBO> IdentitySessions { get; set; }
    public List<AddressCountryResponse> AddressCountryResponseList { get; set; }
}