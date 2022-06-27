using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class UpdatePharmacyLocationHandler : CommandBaseHandler, IRequestHandler<UpdatePharmacyLocationCmd, CmdResponse<UpdatePharmacyLocationCmd>>
{
    public UpdatePharmacyLocationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdatePharmacyLocationCmd>> Handle(UpdatePharmacyLocationCmd request, CancellationToken cancellationToken)
    {
        var existingPharmacyLocation = await _dataLayer.HealthEssentialsContext.PharmacyLocations
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);

        if (existingPharmacyLocation == null)
        {
            return new ()
            {
                Message = $"Pharmacy Location with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var pharmacy = await _dataLayer.HealthEssentialsContext.Pharmacies
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (pharmacy is null)
        {
            return new ()
            {
                Message = $"Pharmacy with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var barangay = await _dataLayer.XnelSystemsContext.AddressBarangays
            .FirstOrDefaultAsync(x => x.Guid == $"{request.BarangayId}", CancellationToken.None);
        
        if (barangay is null)
        {
            return new ()
            {
                Message = $"Barangay with Guid {request.BarangayId} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var city = await _dataLayer.XnelSystemsContext.AddressCities
            .FirstOrDefaultAsync(x => x.Guid == $"{request.CityId}", CancellationToken.None);

        if (city is null)
        {
            return new ()
            {
                Message = $"City with Guid {request.CityId} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var region = await _dataLayer.XnelSystemsContext.AddressRegions
            .FirstOrDefaultAsync(x => x.Guid == $"{request.RegionId}", CancellationToken.None);

        if (region is null)
        {
            return new ()
            {
                Message = $"Region with Guid {request.RegionId} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var province = await _dataLayer.XnelSystemsContext.AddressProvinces
            .FirstOrDefaultAsync(x => x.Guid == $"{request.ProvinceId}", CancellationToken.None);

        if (province is null)
        {
            return new ()
            {
                Message = $"Province with Guid {request.ProvinceId} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var country = await _dataLayer.XnelSystemsContext.AddressCountries
            .FirstOrDefaultAsync(x => x.Guid == $"{request.CountryId}", CancellationToken.None);

        if (country is null)
        {
            return new ()
            {
                Message = $"Country with Guid {request.CountryId} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var updatedPharmacyLocation = request.Adapt(existingPharmacyLocation);
        updatedPharmacyLocation.PharmacyId = pharmacy.Id;
        updatedPharmacyLocation.BarangayId = barangay.Id;
        updatedPharmacyLocation.CityId = city.Id;
        updatedPharmacyLocation.RegionId = region.Id;
        updatedPharmacyLocation.ProvinceId = province.Id;
        updatedPharmacyLocation.CountryId = country.Id;

        _dataLayer.HealthEssentialsContext.Update(updatedPharmacyLocation);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Pharmacy Location with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}