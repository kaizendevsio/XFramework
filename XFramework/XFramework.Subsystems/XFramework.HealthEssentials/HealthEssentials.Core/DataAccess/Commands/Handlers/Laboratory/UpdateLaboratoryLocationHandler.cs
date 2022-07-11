using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class UpdateLaboratoryLocationHandler : CommandBaseHandler, IRequestHandler<UpdateLaboratoryLocationCmd, CmdResponse<UpdateLaboratoryLocationCmd>>
{
    public UpdateLaboratoryLocationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateLaboratoryLocationCmd>> Handle(UpdateLaboratoryLocationCmd request, CancellationToken cancellationToken)
    {
        var existingLaboratoryLocation = await _dataLayer.HealthEssentialsContext.LaboratoryLocations
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (existingLaboratoryLocation is null)
        {
            return new ()
            {
                Message = $"Laboratory Location with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var laboratory = await _dataLayer.HealthEssentialsContext.Laboratories
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (laboratory is null)
        {
            return new ()
            {
                Message = $"Laboratory with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var barangay = await _dataLayer.XnelSystemsContext.AddressBarangays
            .FirstOrDefaultAsync(x => x.Guid == $"{request.BarangayGuid}", CancellationToken.None);
        
        if (barangay is null)
        {
            return new ()
            {
                Message = $"Barangay with Guid {request.BarangayGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var city = await _dataLayer.XnelSystemsContext.AddressCities
            .FirstOrDefaultAsync(x => x.Guid == $"{request.CityGuid}", CancellationToken.None);

        if (city is null)
        {
            return new ()
            {
                Message = $"City with Guid {request.CityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var region = await _dataLayer.XnelSystemsContext.AddressRegions
            .FirstOrDefaultAsync(x => x.Guid == $"{request.RegionGuid}", CancellationToken.None);

        if (region is null)
        {
            return new ()
            {
                Message = $"Region with Guid {request.RegionGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var province = await _dataLayer.XnelSystemsContext.AddressProvinces
            .FirstOrDefaultAsync(x => x.Guid == $"{request.ProvinceGuid}", CancellationToken.None);

        if (province is null)
        {
            return new ()
            {
                Message = $"Province with Guid {request.ProvinceGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var country = await _dataLayer.XnelSystemsContext.AddressCountries
            .FirstOrDefaultAsync(x => x.Guid == $"{request.CountryGuid}", CancellationToken.None);

        if (country is null)
        {
            return new ()
            {
                Message = $"Country with Guid {request.CountryGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var updatedLaboratoryLocation = request.Adapt(existingLaboratoryLocation);
        updatedLaboratoryLocation.Laboratory = laboratory;
        updatedLaboratoryLocation.BarangayId = barangay.Id;
        updatedLaboratoryLocation.CityId = city.Id;
        updatedLaboratoryLocation.RegionId = region.Id;
        updatedLaboratoryLocation.ProvinceId = province.Id;
        updatedLaboratoryLocation.CountryId = country.Id;

        _dataLayer.HealthEssentialsContext.Update(updatedLaboratoryLocation);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Laboratory Location with Guid {request.Guid} has been updated",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}