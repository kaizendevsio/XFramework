using System.Text.Json;
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
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}",CancellationToken.None);

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
            var countryId = response.LaboratoryLocations[index].CountryId;
            var regionId = response.LaboratoryLocations[index].RegionId;
            var provinceId = response.LaboratoryLocations[index].ProvinceId;
            var cityId = response.LaboratoryLocations[index].CityId;
            var barangayId = response.LaboratoryLocations[index].BarangayId;

            var country = _dataLayer.XnelSystemsContext.AddressCountries
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == countryId, CancellationToken.None);
            var region = _dataLayer2.XnelSystemsContext.AddressRegions
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == regionId, CancellationToken.None);
            var province = _dataLayer3.XnelSystemsContext.AddressProvinces
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == provinceId, CancellationToken.None);
            var city = _dataLayer4.XnelSystemsContext.AddressCities
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == cityId, CancellationToken.None);
            var barangay = _dataLayer5.XnelSystemsContext.AddressBarangays
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == barangayId, CancellationToken.None);

            await Task.WhenAll(country, region, province, city, barangay);

            response.LaboratoryLocations[index].Country = country.Result?.Adapt<AddressCountryResponse>();
            response.LaboratoryLocations[index].Region = region.Result?.Adapt<AddressRegionResponse>();
            response.LaboratoryLocations[index].Province = province.Result?.Adapt<AddressProvinceResponse>();
            response.LaboratoryLocations[index].City = city.Result?.Adapt<AddressCityResponse>();
            response.LaboratoryLocations[index].Barangay = barangay?.Result.Adapt<AddressBarangayResponse>();
        }
    }

    private async Task GetMemberList(LaboratoryResponse response)
    {
        for (var index = 0; index < response.LaboratoryMembers.Count; index++)
        {
            response.LaboratoryMembers[index].Laboratory = response;
            response.LaboratoryMembers[index].Credential = _dataLayer.XnelSystemsContext.IdentityCredentials
                .Include(i => i.IdentityInfo)
                .Include(i => i.IdentityContacts)
                .ThenInclude(i => i.Entity)
                .Where(i => i.Guid == response.LaboratoryMembers[index].CredentialId)
                .AsSplitQuery()
                .AsNoTracking()
                .FirstOrDefault()?
                .Adapt<CredentialResponse>();
        }
    }
    
}