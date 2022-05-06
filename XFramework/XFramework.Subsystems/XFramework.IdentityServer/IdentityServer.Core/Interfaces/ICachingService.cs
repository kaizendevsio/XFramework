using IdentityServer.Domain.Generic.Contracts.Responses.Address;
using XFramework.Domain.Generic.Interfaces;

namespace IdentityServer.Core.Interfaces;

public interface ICachingService : IXFrameworkService
{
    public List<IdentitySessionBO> IdentitySessions { get; set; }
    public List<AddressCountryResponse> AddressCountryResponseList { get; set; }
    public List<AddressRegionResponse> AddressRegionResponseList { get; set; }
    public List<AddressProvinceResponse> AddressProvinceResponseList { get; set; }
    public List<AddressCityResponse> AddressCityResponseList { get; set; }
    public List<AddressBarangayResponse> AddressBarangayResponseList { get; set; }
}