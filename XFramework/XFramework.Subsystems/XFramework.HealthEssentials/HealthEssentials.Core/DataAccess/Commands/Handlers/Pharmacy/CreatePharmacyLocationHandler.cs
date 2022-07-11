using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class CreatePharmacyLocationHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyLocationCmd, CmdResponse<CreatePharmacyLocationCmd>>
{
    public CreatePharmacyLocationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<CreatePharmacyLocationCmd>> Handle(CreatePharmacyLocationCmd request, CancellationToken cancellationToken)
    {
        var pharmacy = await _dataLayer.HealthEssentialsContext.Pharmacies
            .FirstOrDefaultAsync(x => x.Guid == $"{request.PharmacyGuid}", CancellationToken.None);
        
        if (pharmacy is null)
        {
            return new ()
            {
                Message = $"Pharmacy with Guid {request.Guid} does not exist",
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

        var pharmacyLocation = request.Adapt<PharmacyLocation>();
        pharmacyLocation.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        pharmacyLocation.PharmacyId = pharmacy.Id;
        pharmacyLocation.BarangayId = barangay.Id;
        pharmacyLocation.CityId = city.Id;
        pharmacyLocation.RegionId = region.Id;
        pharmacyLocation.ProvinceId = province.Id;
        pharmacyLocation.CountryId = country.Id;

        await _dataLayer.HealthEssentialsContext.PharmacyLocations.AddAsync(pharmacyLocation, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(pharmacyLocation.Guid);
        return new ()
        {
            Message = $"Pharmacy Location with Guid {request.Guid} has been created",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}