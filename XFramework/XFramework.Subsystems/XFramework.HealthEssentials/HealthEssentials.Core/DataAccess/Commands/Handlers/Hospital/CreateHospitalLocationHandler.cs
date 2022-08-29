using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class CreateHospitalLocationHandler : CommandBaseHandler, IRequestHandler<CreateHospitalLocationCmd, CmdResponse<CreateHospitalLocationCmd>>
{
    public CreateHospitalLocationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateHospitalLocationCmd>> Handle(CreateHospitalLocationCmd request, CancellationToken cancellationToken)
    {
        var hospital = await _dataLayer.HealthEssentialsContext.Hospitals.FirstOrDefaultAsync(x => x.Guid == $"{request.HospitalGuid}", CancellationToken.None);
        if (hospital is null)
        {
            return new ()
            {
                Message = $"Hospital with Guid {request.HospitalGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var barangay = await _dataLayer.XnelSystemsContext.AddressBarangays.FirstOrDefaultAsync(x => x.Guid == $"{request.BarangayGuid}", CancellationToken.None);
        if (barangay is null)
        {
            return new ()
            {
                Message = $"Barangay with Guid {request.BarangayGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var city = await _dataLayer.XnelSystemsContext.AddressCities.FirstOrDefaultAsync(x => x.Guid == $"{request.CityGuid}", CancellationToken.None);
        if (city is null)
        {
            return new ()
            {
                Message = $"City with Guid {request.CityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var region = await _dataLayer.XnelSystemsContext.AddressRegions.FirstOrDefaultAsync(x => x.Guid == $"{request.RegionGuid}", CancellationToken.None);
        if (region is null)
        {
            return new ()
            {
                Message = $"Region with Guid {request.RegionGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var province = await _dataLayer.XnelSystemsContext.AddressProvinces.FirstOrDefaultAsync(x => x.Guid == $"{request.ProvinceGuid}", CancellationToken.None);
        if (province is null)
        {
            return new ()
            {
                Message = $"Province with Guid {request.ProvinceGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var country = await _dataLayer.XnelSystemsContext.AddressCountries.FirstOrDefaultAsync(x => x.Guid == $"{request.CountryGuid}", CancellationToken.None);
        if (country is null)
        {
            return new ()
            {
                Message = $"Country with Guid {request.CountryGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var hospitalLocation = request.Adapt<HospitalLocation>();
        hospitalLocation.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        hospitalLocation.Hospital = hospital;
        hospitalLocation.BarangayGuid = barangay.Guid;
        hospitalLocation.CityGuid = city.Guid;
        hospitalLocation.RegionGuid = region.Guid;
        hospitalLocation.ProvinceGuid = province.Guid;
        hospitalLocation.CountryGuid = country.Guid;
        
        await _dataLayer.HealthEssentialsContext.HospitalLocations.AddAsync(hospitalLocation, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(hospitalLocation.Guid);
        return new()
        {
            Message = $"Hospital Location with Guid {hospitalLocation.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}