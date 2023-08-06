using HealthEssentials.Core.DataAccess.Query.Entity.Laboratory;
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
            .Include(i => i.Type) 
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
                
            };
        }
        
        var response = laboratory.Adapt<List<LaboratoryResponse>>();
        await GetBranchList(response);
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Laboratory Found",
            
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
                
                var countryGuid = location.CountryGuid;
                var regionGuid = location.RegionGuid;
                var provinceGuid = location.ProvinceGuid;
                var cityGuid = location.CityGuid;
                var barangayGuid = location.BarangayGuid;

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

                location.Country = country.Result?.Adapt<AddressCountryResponse>();
                location.Region = region.Result?.Adapt<AddressRegionResponse>();
                location.Province = province.Result?.Adapt<AddressProvinceResponse>();
                location.City = city.Result?.Adapt<AddressCityResponse>();
                location.Barangay = barangay?.Result.Adapt<AddressBarangayResponse>();
            }
        }
    }
}