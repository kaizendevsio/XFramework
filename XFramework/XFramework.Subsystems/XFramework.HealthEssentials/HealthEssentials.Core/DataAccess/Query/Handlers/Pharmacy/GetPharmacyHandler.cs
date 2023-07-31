using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;
using HealthEssentials.Domain.Generics.Contracts.Responses.Storage;
using IdentityServer.Domain.Generic.Contracts.Responses;
using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyHandler : QueryBaseHandler, IRequestHandler<GetPharmacyQuery, QueryResponse<PharmacyResponse>>
{
    public GetPharmacyHandler(IDataLayer dataLayer, IDataLayer dataLayer2, IDataLayer dataLayer3, IDataLayer dataLayer4, IDataLayer dataLayer5)
    {
        _dataLayer2 = dataLayer2;
        _dataLayer3 = dataLayer3;
        _dataLayer4 = dataLayer4;
        _dataLayer5 = dataLayer5;
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<PharmacyResponse>> Handle(GetPharmacyQuery request, CancellationToken cancellationToken)
    {
        var pharmacy = await _dataLayer.HealthEssentialsContext.Pharmacies
            .Include(i => i.Entity)
            .Include(i => i.PharmacyLocations)
            .Include(i => i.PharmacyMembers)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}",CancellationToken.None);

        if (pharmacy is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = $"Pharmacy not found",
                
            };
        }
        
        var response = pharmacy.Adapt<PharmacyResponse>();

        await GetMemberList(response);
        await GetBranchList(response);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Pharmacy found",
            
            Response = response
        };        
    }

    private async Task GetBranchList(PharmacyResponse response)
    {
        for (var index = 0; index < response.PharmacyLocations.Count; index++)
        {
            var countryGuid = response.PharmacyLocations[index].CountryGuid;
            var regionGuid = response.PharmacyLocations[index].RegionGuid;
            var provinceGuid = response.PharmacyLocations[index].ProvinceGuid;
            var cityGuid = response.PharmacyLocations[index].CityGuid;
            var barangayGuid = response.PharmacyLocations[index].BarangayGuid;

            var country = _dataLayer.XnelSystemsContext.AddressCountries.AsNoTracking().FirstOrDefaultAsync(x => x.Guid == $"{countryGuid}", CancellationToken.None);
            var region = _dataLayer2.XnelSystemsContext.AddressRegions.AsNoTracking().FirstOrDefaultAsync(x => x.Guid == $"{regionGuid}", CancellationToken.None);
            var province = _dataLayer3.XnelSystemsContext.AddressProvinces.AsNoTracking().FirstOrDefaultAsync(x => x.Guid == $"{provinceGuid}", CancellationToken.None);
            var city = _dataLayer4.XnelSystemsContext.AddressCities.AsNoTracking().FirstOrDefaultAsync(x => x.Guid == $"{cityGuid}", CancellationToken.None);
            var barangay = _dataLayer5.XnelSystemsContext.AddressBarangays.AsNoTracking().FirstOrDefaultAsync(x => x.Guid == $"{barangayGuid}", CancellationToken.None);

            await Task.WhenAll(country, region, province, city, barangay);

            response.PharmacyLocations[index].Country = country.Result?.Adapt<AddressCountryResponse>();
            response.PharmacyLocations[index].Region = region.Result?.Adapt<AddressRegionResponse>();
            response.PharmacyLocations[index].Province = province.Result?.Adapt<AddressProvinceResponse>();
            response.PharmacyLocations[index].City = city.Result?.Adapt<AddressCityResponse>();
            response.PharmacyLocations[index].Barangay = barangay?.Result.Adapt<AddressBarangayResponse>();
        }
    }

    private async Task GetMemberList(PharmacyResponse response)
    {
        for (var index = 0; index < response.PharmacyMembers.Count; index++)
        {
            response.PharmacyMembers[index].Credential = _dataLayer.XnelSystemsContext.IdentityCredentials
                .AsNoTracking()
                .Include(i => i.IdentityInfo)
                .Include(i => i.IdentityContacts)
                .ThenInclude(i => i.Entity)
                .AsSplitQuery()
                .Where(i => i.Guid == response.PharmacyMembers[index].CredentialGuid)
                .FirstOrDefault()?
                .Adapt<CredentialResponse>();
        }
    }

}