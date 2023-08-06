using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Domain.Generics.Contracts;
using XFramework.Domain.Generic.Enums;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class CreateLaboratoryLocationHandler : CommandBaseHandler, IRequestHandler<CreateLaboratoryLocationCmd, CmdResponse<CreateLaboratoryLocationCmd>>
{
    public CreateLaboratoryLocationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLaboratoryLocationCmd>> Handle(CreateLaboratoryLocationCmd request, CancellationToken cancellationToken)
    {
        var laboratory = await _dataLayer.HealthEssentialsContext.Laboratories.FirstOrDefaultAsync(x => x.Guid == $"{request.LaboratoryGuid}", CancellationToken.None);
        if (laboratory is null)
        {
            return new ()
            {
                Message = $"Laboratory with Guid {request.LaboratoryGuid} does not exist",
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
        
        var laboratoryLocation = request.Adapt<LaboratoryLocation>();
        laboratoryLocation.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        laboratoryLocation.Laboratory = laboratory;
        laboratoryLocation.BarangayGuid = barangay.Guid;
        laboratoryLocation.CityGuid = city.Guid;
        laboratoryLocation.RegionGuid = region.Guid;
        laboratoryLocation.ProvinceGuid = province.Guid;
        laboratoryLocation.CountryGuid = country.Guid;
        laboratoryLocation.Status = (int?) (request.Status is GenericStatusType.None ? GenericStatusType.Pending : request.Status);

        await _dataLayer.HealthEssentialsContext.LaboratoryLocations.AddAsync(laboratoryLocation, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(laboratoryLocation.Guid);
        return new ()
        {
            Message = $"Laboratory Location with Guid {laboratoryLocation.Guid} has been created",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}