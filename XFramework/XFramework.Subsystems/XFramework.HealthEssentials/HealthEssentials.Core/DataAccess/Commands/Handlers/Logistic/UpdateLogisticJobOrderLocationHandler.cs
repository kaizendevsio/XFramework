using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Logistic;

public class UpdateLogisticJobOrderLocationHandler : CommandBaseHandler, IRequestHandler<UpdateLogisticJobOrderLocationCmd, CmdResponse<UpdateLogisticJobOrderLocationCmd>>
{
    public UpdateLogisticJobOrderLocationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateLogisticJobOrderLocationCmd>> Handle(UpdateLogisticJobOrderLocationCmd request, CancellationToken cancellationToken)
    {
        var existingLocation = await _dataLayer.HealthEssentialsContext.LogisticJobOrderLocations.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingLocation == null)
        {
            return new()
            {
                Message = $"Location with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedLocation = request.Adapt(existingLocation);

        if (request.LogisticJobOrderGuid is not null)
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
            updatedLocation.LogisticJobOrder = jobOrder;
        }

        if (request.BarangayGuid is not null)
        {
            var barangay = await _dataLayer.XnelSystemsContext.AddressBarangays.FirstOrDefaultAsync(x => x.Guid == $"{request.BarangayGuid}", CancellationToken.None);
            if (barangay is null)
            {
                return new()
                {
                    Message = $"Barangay with Guid {request.BarangayGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedLocation.Barangay = barangay.Id;
        }

        if (request.CityGuid is not null)
        {
            var city = await _dataLayer.XnelSystemsContext.AddressCities.FirstOrDefaultAsync(x => x.Guid == $"{request.CityGuid}", CancellationToken.None);
            if (city is null)
            {
                return new()
                {
                    Message = $"City with Guid {request.CityGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedLocation.City = city.Id;
        }

        if (request.RegionGuid is not null)
        {
            var region = await _dataLayer.XnelSystemsContext.AddressRegions.FirstOrDefaultAsync(x => x.Guid == $"{request.RegionGuid}", CancellationToken.None);
            if (region is null)
            {
                return new()
                {
                    Message = $"Region with Guid {request.RegionGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedLocation.Region = region.Id;
        }

        if (request.ProvinceGuid is not null)
        {
            var province = await _dataLayer.XnelSystemsContext.AddressProvinces.FirstOrDefaultAsync(x => x.Guid == $"{request.ProvinceGuid}", CancellationToken.None);
            if (province is null)
            {
                return new()
                {
                    Message = $"Province with Guid {request.ProvinceGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedLocation.Province = province.Id;
        }

        if (request.CountryGuid is not null)
        {
            var country = await _dataLayer.XnelSystemsContext.AddressCountries.FirstOrDefaultAsync(x => x.Guid == $"{request.CountryGuid}", CancellationToken.None);
            if (country is null)
            {
                return new()
                {
                    Message = $"Country with Guid {request.CountryGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedLocation.Country = country.Id;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedLocation);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Location with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}