using HealthEssentials.Core.DataAccess.Query.Entity.Pharmacy;
using HealthEssentials.Domain.Generics.Contracts.Responses.Storage;
using IdentityServer.Domain.Generic.Contracts.Responses;
using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Pharmacy;

public class GetPharmacyLocationHandler : QueryBaseHandler, IRequestHandler<GetPharmacyLocationQuery, QueryResponse<PharmacyLocationResponse>>
{
    public GetPharmacyLocationHandler(IDataLayer dataLayer, IDataLayer dataLayer2, IDataLayer dataLayer3, IDataLayer dataLayer4, IDataLayer dataLayer5)
    {
        _dataLayer2 = dataLayer2;
        _dataLayer3 = dataLayer3;
        _dataLayer4 = dataLayer4;
        _dataLayer5 = dataLayer5;
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<PharmacyLocationResponse>> Handle(GetPharmacyLocationQuery request, CancellationToken cancellationToken)
    {
        var pharmacyLocation = await _dataLayer.HealthEssentialsContext.PharmacyLocations
            .Include(i => i.PharmacyJobOrders)
            .Include(i => i.PharmacyServices)
            .Include(i => i.PharmacyMembers)
            .Include(i => i.Pharmacy)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}",CancellationToken.None);

        if (pharmacyLocation is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = $"Laboratory with the Guid '{request.Guid}' does not exist",
                IsSuccess = true
            };
        }
        
        var response = pharmacyLocation.Adapt<PharmacyLocationResponse>();

        await GetFilesList(response);
        await GetMemberList(response);
        await GetBranchAddressData(response);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Laboratory Found",
            IsSuccess = true,
            Response = response
        };        
    }

    private async Task GetFilesList(PharmacyLocationResponse response)
    {
        response.Files = _dataLayer.XnelSystemsContext.StorageFiles
            .Where(i => i.IdentifierGuid == $"{response.Guid}" || i.IdentifierGuid == $"{response.Pharmacy.Guid}")
            .AsNoTracking()
            .ToList()
            .Adapt<List<StorageFileResponse>>();
    }
    private async Task GetBranchAddressData(PharmacyLocationResponse response)
    {
        var countryId = response.CountryId;
        var regionId = response.RegionId;
        var provinceId = response.ProvinceId;
        var cityId = response.CityId;
        var barangayId = response.BarangayId;

        var country = _dataLayer.XnelSystemsContext.AddressCountries.AsNoTracking().FirstOrDefaultAsync(i => i.Id == countryId, CancellationToken.None);
        var region = _dataLayer2.XnelSystemsContext.AddressRegions.AsNoTracking().FirstOrDefaultAsync(i => i.Id == regionId, CancellationToken.None);
        var province = _dataLayer3.XnelSystemsContext.AddressProvinces.AsNoTracking().FirstOrDefaultAsync(i => i.Id == provinceId, CancellationToken.None);
        var city = _dataLayer4.XnelSystemsContext.AddressCities.AsNoTracking().FirstOrDefaultAsync(i => i.Id == cityId, CancellationToken.None);
        var barangay = _dataLayer5.XnelSystemsContext.AddressBarangays.AsNoTracking().FirstOrDefaultAsync(i => i.Id == barangayId, CancellationToken.None);

        await Task.WhenAll(country, region, province, city, barangay);

        response.Country = country.Result?.Adapt<AddressCountryResponse>();
        response.Region = region.Result?.Adapt<AddressRegionResponse>();
        response.Province = province.Result?.Adapt<AddressProvinceResponse>();
        response.City = city.Result?.Adapt<AddressCityResponse>();
        response.Barangay = barangay?.Result.Adapt<AddressBarangayResponse>();
    }

    private async Task GetMemberList(PharmacyLocationResponse response)
    {
        for (var index = 0; index < response.PharmacyMembers.Count; index++)
        {
            var o = index; 
            response.PharmacyMembers[o].Credential = _dataLayer.XnelSystemsContext.IdentityCredentials
                .Include(i => i.IdentityInfo)
                .Include(i => i.IdentityContacts)
                .ThenInclude(i => i.Entity)
                .Where(i => i.Id == response.PharmacyMembers[o].CredentialId)
                .AsSplitQuery()
                .AsNoTracking()
                .FirstOrDefault()?
                .Adapt<CredentialResponse>();
        }
    }
    
}