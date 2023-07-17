using System.Text.Json;
using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Responses.Storage;
using IdentityServer.Domain.Generic.Contracts.Responses;
using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryLocationHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryLocationQuery, QueryResponse<LaboratoryLocationResponse>>
{
    public GetLaboratoryLocationHandler(IDataLayer dataLayer, IDataLayer dataLayer2, IDataLayer dataLayer3, IDataLayer dataLayer4, IDataLayer dataLayer5)
    {
        _dataLayer2 = dataLayer2;
        _dataLayer3 = dataLayer3;
        _dataLayer4 = dataLayer4;
        _dataLayer5 = dataLayer5;
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<LaboratoryLocationResponse>> Handle(GetLaboratoryLocationQuery request, CancellationToken cancellationToken)
    {
        var laboratoryLocation = await _dataLayer.HealthEssentialsContext.LaboratoryLocations
            .Include(i => i.LaboratoryServices)
            .Include(i => i.LaboratoryMembers)
            .Include(i => i.LaboratoryLocationTags)
            .Include(i => i.Laboratory)
            .Where(i => i.Guid == $"{request.Guid}")
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(CancellationToken.None);

        if (laboratoryLocation is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = $"Laboratory with the Guid '{request.Guid}' does not exist",
                
            };
        }
        
        var response = laboratoryLocation.Adapt<LaboratoryLocationResponse>();

        await GetFilesList(response);
        await GetMemberList(response);
        await GetBranchAddressData(response);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Laboratory Found",
            
            Response = response
        };        
    }

    private async Task GetFilesList(LaboratoryLocationResponse response)
    {
        response.Files = _dataLayer.XnelSystemsContext.StorageFiles
            .Where(i => i.IdentifierGuid == $"{response.Guid}" || i.IdentifierGuid == $"{response.Laboratory.Guid}")
            .AsNoTracking()
            .ToList()
            .Adapt<List<StorageFileResponse>>();
    }
    private async Task GetBranchAddressData(LaboratoryLocationResponse response)
    {
        var countryGuid = response.CountryGuid;
        var regionGuid = response.RegionGuid;
        var provinceGuid = response.ProvinceGuid;
        var cityGuid = response.CityGuid;
        var barangayGuid = response.BarangayGuid;

        var country = _dataLayer.XnelSystemsContext.AddressCountries
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{countryGuid}", CancellationToken.None);
        var region = _dataLayer2.XnelSystemsContext.AddressRegions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{regionGuid}", CancellationToken.None);
        var province = _dataLayer3.XnelSystemsContext.AddressProvinces
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{provinceGuid}", CancellationToken.None);
        var city = _dataLayer4.XnelSystemsContext.AddressCities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{cityGuid}", CancellationToken.None);
        var barangay = _dataLayer5.XnelSystemsContext.AddressBarangays
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{barangayGuid}", CancellationToken.None);

        await Task.WhenAll(country, region, province, city, barangay);

        response.Country = country.Result?.Adapt<AddressCountryResponse>();
        response.Region = region.Result?.Adapt<AddressRegionResponse>();
        response.Province = province.Result?.Adapt<AddressProvinceResponse>();
        response.City = city.Result?.Adapt<AddressCityResponse>();
        response.Barangay = barangay?.Result.Adapt<AddressBarangayResponse>();
    }

    private async Task GetMemberList(LaboratoryLocationResponse response)
    {
        for (var index = 0; index < response.LaboratoryMembers.Count; index++)
        {
            var o = index; 
            response.LaboratoryMembers[o].Credential = _dataLayer.XnelSystemsContext.IdentityCredentials
                .Include(i => i.IdentityInfo)
                .Include(i => i.IdentityContacts)
                .ThenInclude(i => i.Entity)
                .Where(i => i.Guid == response.LaboratoryMembers[o].CredentialGuid)
                .AsSplitQuery()
                .AsNoTracking()
                .FirstOrDefault()?
                .Adapt<CredentialResponse>();
        }
    }
    
}