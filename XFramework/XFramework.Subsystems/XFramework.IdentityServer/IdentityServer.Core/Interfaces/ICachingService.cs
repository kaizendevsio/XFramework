using IdentityServer.Domain.Generic.Contracts.Responses.Address;
using XFramework.Domain.Generic.Interfaces;

namespace IdentityServer.Core.Interfaces;

public interface ICachingService : IXFrameworkService
{
    public List<IdentitySessionBO> IdentitySessions { get; set; }
    public List<AddressCountryResponse> AddressCountryResponseList { get; set; }
}