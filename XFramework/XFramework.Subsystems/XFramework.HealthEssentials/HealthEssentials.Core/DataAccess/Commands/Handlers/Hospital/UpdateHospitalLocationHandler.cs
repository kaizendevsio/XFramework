using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class UpdateHospitalLocationHandler : CommandBaseHandler, IRequestHandler<UpdateHospitalLocationCmd, CmdResponse<UpdateHospitalLocationCmd>>
{
    public UpdateHospitalLocationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateHospitalLocationCmd>> Handle(UpdateHospitalLocationCmd request, CancellationToken cancellationToken)
    {
        var existingHospitalLocation = await _dataLayer.HealthEssentialsContext.HospitalLocations.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingHospitalLocation is null)
        {
            return new ()
            {
                Message = $"Hospital Location with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedHospitalLocation = request.Adapt(existingHospitalLocation);

        if (request.HospitalGuid is null)
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
            updatedHospitalLocation.Hospital = hospital;
        }

        if (request.BarangayGuid is null)
        {
            var barangay = await _dataLayer.XnelSystemsContext.AddressBarangays.FirstOrDefaultAsync(x => x.Guid == $"{request.BarangayGuid}", CancellationToken.None);
            if (barangay is null)
            {
                return new ()
                {
                    Message = $"Barangay with Guid {request.BarangayGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedHospitalLocation.Barangay = barangay.Id;
        }

        if (request.CityGuid is null)
        {
            var city = await _dataLayer.XnelSystemsContext.AddressCities.FirstOrDefaultAsync(x => x.Guid == $"{request.CityGuid}", CancellationToken.None);
            if (city is null)
            {
                return new ()
                {
                    Message = $"City with Guid {request.CityGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedHospitalLocation.City = city.Id;
        }

        if (request.RegionGuid is null)
        {
            var region = await _dataLayer.XnelSystemsContext.AddressRegions.FirstOrDefaultAsync(x => x.Guid == $"{request.RegionGuid}", CancellationToken.None);
            if (region is null)
            {
                return new ()
                {
                    Message = $"Region with Guid {request.RegionGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedHospitalLocation.Region = region.Id;
        }

        if (request.ProvinceGuid is null)
        {
            var province = await _dataLayer.XnelSystemsContext.AddressProvinces.FirstOrDefaultAsync(x => x.Guid == $"{request.ProvinceGuid}", CancellationToken.None);
            if (province is null)
            {
                return new ()
                {
                    Message = $"Province with Guid {request.ProvinceGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedHospitalLocation.Province = province.Id;
        }

        if (request.CountryGuid is null)
        {
            var country = await _dataLayer.XnelSystemsContext.AddressCountries.FirstOrDefaultAsync(x => x.Guid == $"{request.CountryGuid}", CancellationToken.None);
            if (country is null)
            {
                return new ()
                {
                    Message = $"Country with Guid {request.CountryGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedHospitalLocation.Country = country.Id;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedHospitalLocation);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(cancellationToken);
        
        return new ()
        {
            Message = $"Hospital Location with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}