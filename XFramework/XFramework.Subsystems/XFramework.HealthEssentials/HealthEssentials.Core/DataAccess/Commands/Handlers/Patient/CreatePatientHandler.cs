using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class CreatePatientHandler : CommandBaseHandler, IRequestHandler<CreatePatientCmd, CmdResponse<CreatePatientCmd>>
{
    public CreatePatientHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreatePatientCmd>> Handle(CreatePatientCmd request, CancellationToken cancellationToken)
    {
        var credential = await _dataLayer.XnelSystemsContext.IdentityCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.CredentialGuid}", cancellationToken: cancellationToken);
       
        if (credential is null)
        {
            return new ()
            {
                Message = $"Credential with Guid {request.CredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        if (request.Address is not null)
        {
            var address = _dataLayer.XnelSystemsContext.IdentityAddresses.FirstOrDefaultAsync(i => i.AddressEntities.Name == $"Home", cancellationToken: cancellationToken);
            var addressEntity = _dataLayer.XnelSystemsContext.IdentityAddressEntities.FirstOrDefaultAsync(i => i.Name == $"Home", cancellationToken: cancellationToken);

            var country = _dataLayer.XnelSystemsContext.AddressCountries.FirstOrDefaultAsync(i => i.Guid == $"{request.Address.CountryGuid}", cancellationToken: cancellationToken);
            var region = _dataLayer.XnelSystemsContext.AddressRegions.FirstOrDefaultAsync(i => i.Guid == $"{request.Address.RegionGuid}", cancellationToken: cancellationToken);
            var province = _dataLayer.XnelSystemsContext.AddressProvinces.FirstOrDefaultAsync(i => i.Guid == $"{request.Address.ProvinceGuid}", cancellationToken: cancellationToken);
            var city = _dataLayer.XnelSystemsContext.AddressCities.FirstOrDefaultAsync(i => i.Guid == $"{request.Address.CityGuid}", cancellationToken: cancellationToken);
            var barangay = _dataLayer.XnelSystemsContext.AddressBarangays.FirstOrDefaultAsync(i => i.Guid == $"{request.Address.BarangayGuid}", cancellationToken: cancellationToken);
            
            await Task.WhenAll(address, addressEntity,country, region, province, city, barangay);

            if (addressEntity.Result is null)
            {
                await _dataLayer.XnelSystemsContext.IdentityAddresses.AddAsync(new()
                {
                    IdentityInfoId = credential.IdentityInfoId,
                    Barangay = barangay.Result?.Id,
                    City = city.Result?.Id,
                    Region = region.Result?.Id,
                    AddressEntitiesId = addressEntity.Result?.Id,
                    DefaultAddress = true,
                    Province = province.Result?.Id,
                    Country = country.Result?.Id,
                    Guid = $"{Guid.NewGuid()}",
                }, CancellationToken.None);
            }
        }

        var entity = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.Patient>();
        entity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        entity.EntityId = 1;
        entity.CredentialId = credential.Id;
        
        _dataLayer.HealthEssentialsContext.Patients.Add(entity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Patient with Guid {entity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(entity.Guid)
            }
        };
    }
}