using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Address;

namespace IdentityServer.Core.DataAccess.Commands.Handlers.Identity.Address;

public class CreateAddressHandler : CommandBaseHandler ,IRequestHandler<CreateAddressCmd, CmdResponse<CreateAddressCmd>>
{
    public CreateAddressHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateAddressCmd>> Handle(CreateAddressCmd request, CancellationToken cancellationToken)
    {
        var addressEntity = await _dataLayer.IdentityAddressEntities.FirstOrDefaultAsync(i => i.Guid == $"{request.AddressEntityGuid}", cancellationToken);
        if (addressEntity == null)
        {
            return new ()
            {
                Message = $"Address entity with Guid {request.AddressEntityGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var identityInformation = await _dataLayer.IdentityInformations.FirstOrDefaultAsync(i => i.Guid == $"{request.IdentityInfoGuid}", cancellationToken);
        if (identityInformation == null)
        {
            return new ()
            {
                Message = $"Identity information with Guid {request.IdentityInfoGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var country = await _dataLayer.AddressCountries.FirstOrDefaultAsync(i => i.Guid == $"{request.CountryGuid}", cancellationToken: cancellationToken);
        var region = await _dataLayer.AddressRegions.FirstOrDefaultAsync(i => i.Guid == $"{request.RegionGuid}", cancellationToken: cancellationToken);
        var province = await _dataLayer.AddressProvinces.FirstOrDefaultAsync(i => i.Guid == $"{request.ProvinceGuid}", cancellationToken: cancellationToken);
        var city = await _dataLayer.AddressCities.FirstOrDefaultAsync(i => i.Guid == $"{request.CityGuid}", cancellationToken: cancellationToken);
        var barangay = await _dataLayer.AddressBarangays.FirstOrDefaultAsync(i => i.Guid == $"{request.BarangayGuid}", cancellationToken: cancellationToken);

        if (country is null)
        {
            return new ()
            {
                Message = $"Country with Guid {request.CountryGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        if (region is null)
        {
            return new ()
            {
                Message = $"Region with Guid {request.RegionGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        if (province is null)
        {
            return new()
            {
                Message = $"Province with Guid {request.ProvinceGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }    
        
        if (city is null)
        {
            return new()
            {
                Message = $"City with Guid {request.CityGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        if (barangay is null)
        {
            return new()
            {
                Message = $"Barangay with Guid {request.BarangayGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var address = request.Adapt<IdentityAddress>();
        address.CountryNavigation = country;
        address.RegionNavigation = region;
        address.ProvinceNavigation = province;
        address.CityNavigation = city;
        address.BarangayNavigation = barangay;
        
        address.AddressEntities = addressEntity;
        address.IdentityInfo = identityInformation;

        await _dataLayer.IdentityAddresses.AddAsync(address, CancellationToken.None);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Request = addressEntity.Adapt<CreateAddressCmd>()
        };

    }
}