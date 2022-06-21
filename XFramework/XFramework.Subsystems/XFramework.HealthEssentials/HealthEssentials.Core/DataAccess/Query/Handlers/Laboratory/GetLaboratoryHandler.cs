using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;
using HealthEssentials.Domain.Generics.Contracts.Responses.Storage;
using IdentityServer.Domain.Generic.Contracts.Responses;
using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryQuery, QueryResponse<LaboratoryResponse>>
{
    public GetLaboratoryHandler(IDataLayer dataLayer, IDataLayer dataLayer2, IDataLayer dataLayer3, IDataLayer dataLayer4, IDataLayer dataLayer5)
    {
        _dataLayer2 = dataLayer2;
        _dataLayer3 = dataLayer3;
        _dataLayer4 = dataLayer4;
        _dataLayer5 = dataLayer5;
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<LaboratoryResponse>> Handle(GetLaboratoryQuery request, CancellationToken cancellationToken)
    {
        var laboratory = await _dataLayer.HealthEssentialsContext.Laboratories
            .Include(i => i.LaboratoryLocations)
            .Include(i => i.LaboratoryMembers)
            .Where(i => i.Guid == $"{request.Guid}")
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(CancellationToken.None);

        if (laboratory is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = $"Laboratory with the Guid '{request.Guid}' does not exist",
                IsSuccess = true
            };
        }
        
        var response = laboratory.Adapt<LaboratoryResponse>();

        response.Files = GetFilesList(response);
        await GetMemberList(response);
        await GetBranchList(response);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Laboratory Found",
            IsSuccess = true,
            Response = response
        };        
    }

    private async Task GetBranchList(LaboratoryResponse response)
    {
        for (var index = 0; index < response.LaboratoryLocations.Count; index++)
        {
            var countryId = response.LaboratoryLocations[index].Country;
            var regionId = response.LaboratoryLocations[index].Region;
            var provinceId = response.LaboratoryLocations[index].Province;
            var cityId = response.LaboratoryLocations[index].City;
            var barangayId = response.LaboratoryLocations[index].Barangay;

            var countryNavigation = _dataLayer.XnelSystemsContext.AddressCountries
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == int.Parse(countryId), CancellationToken.None);
            var regionNavigation = _dataLayer2.XnelSystemsContext.AddressRegions
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == int.Parse(countryId), CancellationToken.None);
            var provinceNavigation = _dataLayer3.XnelSystemsContext.AddressProvinces
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == int.Parse(countryId), CancellationToken.None);
            var cityNavigation = _dataLayer4.XnelSystemsContext.AddressCities
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == int.Parse(countryId), CancellationToken.None);
            var barangayNavigation = _dataLayer5.XnelSystemsContext.AddressBarangays
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == int.Parse(countryId), CancellationToken.None);

            await Task.WhenAll(countryNavigation, regionNavigation, provinceNavigation, cityNavigation, barangayNavigation);

            response.LaboratoryLocations[index].CountryNavigation =
                countryNavigation.Result?.Adapt<AddressCountryResponse>();
            response.LaboratoryLocations[index].RegionNavigation = regionNavigation.Result?.Adapt<AddressRegionResponse>();
            response.LaboratoryLocations[index].ProvinceNavigation =
                provinceNavigation.Result?.Adapt<AddressProvinceResponse>();
            response.LaboratoryLocations[index].CityNavigation = cityNavigation.Result?.Adapt<AddressCityResponse>();
            response.LaboratoryLocations[index].BarangayNavigation =
                barangayNavigation?.Result.Adapt<AddressBarangayResponse>();
        }
    }

    private async Task GetMemberList(LaboratoryResponse response)
    {
        for (var index = 0; index < response.LaboratoryMembers.Count; index++)
        {
            response.LaboratoryMembers[index].Credential = _dataLayer.XnelSystemsContext.IdentityCredentials
                .AsNoTracking()
                .Include(i => i.IdentityInfo)
                .Include(i => i.IdentityContacts)
                .ThenInclude(i => i.Entity)
                .AsSplitQuery()
                .Where(i => i.Id == response.LaboratoryMembers[index].CredentialId)
                .FirstOrDefault()?
                .Adapt<CredentialResponse>();
        }
    }

    private List<StorageFileResponse> GetFilesList(LaboratoryResponse response)
    {
        return _dataLayer.XnelSystemsContext.StorageFiles
            .Where(i => i.IdentifierGuid == response.Guid)
            .AsNoTracking()
            .ToList()
            .Adapt<List<StorageFileResponse>>();
    }
}