using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;
using HealthEssentials.Domain.DataTransferObjects;
using HealthEssentials.Domain.Generics.Contracts.Responses.Storage;
using IdentityServer.Domain.Generic.Contracts.Responses;
using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Laboratory;

public class GetLaboratoryListHandler : QueryBaseHandler, IRequestHandler<GetLaboratoryListQuery, QueryResponse<List<LaboratoryResponse>>>
{

    public GetLaboratoryListHandler(IDataLayer dataLayer, IDataLayer dataLayer2, IDataLayer dataLayer3, IDataLayer dataLayer4, IDataLayer dataLayer5)
    {
        _dataLayer5 = dataLayer5;
        _dataLayer2 = dataLayer2;
        _dataLayer3 = dataLayer3;
        _dataLayer4 = dataLayer4;
        _dataLayer = dataLayer;
    }

    public async Task<QueryResponse<List<LaboratoryResponse>>> Handle(GetLaboratoryListQuery request, CancellationToken cancellationToken)
    {
        var laboratory = await _dataLayer.HealthEssentialsContext.Laboratories
            .Include(i => i.Entity) 
            .ThenInclude(i => i.Group)
            .Include(i => i.LaboratoryLocations) 
            .Where(i => EF.Functions.ILike(i.Name, $"%{request.SearchField}%"))
            .Where(i => i.Status == (int) request.Status)
            .OrderBy(i => i.Name)
            .Take(request.PageSize)
            .AsNoTracking()
            .AsSplitQuery()
            .ToListAsync(CancellationToken.None);

        if (!laboratory.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No Laboratory Found",
                IsSuccess = true
            };
        }
        
        var response = laboratory.Adapt<List<LaboratoryResponse>>();
        await GetBranchList(response);
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Laboratory Found",
            IsSuccess = true,
            Response = response
        };
    }
    
    private async Task GetBranchList(List<LaboratoryResponse> response)
    {
        for (var index = 0; index < response.Count; index++)
        {
            var laboratoryResponse = response[index];

            for (var i = 0; i < laboratoryResponse.LaboratoryLocations.Count; i++)
            {
                var location = laboratoryResponse.LaboratoryLocations[i];
                
                var countryId = location.CountryId;
                var regionId = location.RegionId;
                var provinceId = location.ProvinceId;
                var cityId = location.CityId;
                var barangayId = location.BarangayId;

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

                location.Country = country.Result?.Adapt<AddressCountryResponse>();
                location.Region = region.Result?.Adapt<AddressRegionResponse>();
                location.Province = province.Result?.Adapt<AddressProvinceResponse>();
                location.City = city.Result?.Adapt<AddressCityResponse>();
                location.Barangay = barangay?.Result.Adapt<AddressBarangayResponse>();
            }
        }
    }
}