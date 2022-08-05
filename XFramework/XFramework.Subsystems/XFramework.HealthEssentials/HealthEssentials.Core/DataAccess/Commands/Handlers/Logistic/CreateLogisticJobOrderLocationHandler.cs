using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class CreateLogisticJobOrderLocationHandler : CommandBaseHandler, IRequestHandler<CreateLogisticJobOrderLocationCmd, CmdResponse<CreateLogisticJobOrderLocationCmd>>
{
    public CreateLogisticJobOrderLocationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLogisticJobOrderLocationCmd>> Handle(CreateLogisticJobOrderLocationCmd request, CancellationToken cancellationToken)
    {
        var jobOrder = await _dataLayer.HealthEssentialsContext.LogisticJobOrders.FirstOrDefaultAsync(x => x.Guid == $"{request.LogisticJobOrderGuid}", CancellationToken.None);
        if (jobOrder is null)
        {
            return new()
            {
                Message = $"Logistic Job Order with Guid {request.LogisticJobOrderGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var barangay = await _dataLayer.XnelSystemsContext.AddressBarangays.FirstOrDefaultAsync(x => x.Guid == $"{request.BarangayGuid}", CancellationToken.None);
        if (barangay is null)
        {
            return new()
            {
                Message = $"Barangay with Guid {request.BarangayGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var city = await _dataLayer.XnelSystemsContext.AddressCities.FirstOrDefaultAsync(x => x.Guid == $"{request.CityGuid}", CancellationToken.None);
        if (city is null)
        {
            return new()
            {
                Message = $"City with Guid {request.CityGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var region = await _dataLayer.XnelSystemsContext.AddressRegions.FirstOrDefaultAsync(x => x.Guid == $"{request.RegionGuid}", CancellationToken.None);
        if (region is null)
        {
            return new()
            {
                Message = $"Region with Guid {request.RegionGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var province = await _dataLayer.XnelSystemsContext.AddressProvinces.FirstOrDefaultAsync(x => x.Guid == $"{request.ProvinceGuid}", CancellationToken.None);
        if (province is null)
        {
            return new()
            {
                Message = $"Province with Guid {request.ProvinceGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var country = await _dataLayer.XnelSystemsContext.AddressCountries.FirstOrDefaultAsync(x => x.Guid == $"{request.CountryGuid}", CancellationToken.None);
        if (country is null)
        {
            return new()
            {
                Message = $"Country with Guid {request.CountryGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var location = request.Adapt<LogisticJobOrderLocation>();
        location.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        location.LogisticJobOrder = jobOrder;
        location.Barangay = barangay.Id;
        location.City = city.Id;
        location.Region = region.Id;
        location.Province = province.Id;
        location.Country = country.Id;
        
        await _dataLayer.HealthEssentialsContext.LogisticJobOrderLocations.AddAsync(location, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(location.Guid);
        return new()
        {
            Message = $"Logistic Job Order Location with Guid {location.Guid} created",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
        


    }
}