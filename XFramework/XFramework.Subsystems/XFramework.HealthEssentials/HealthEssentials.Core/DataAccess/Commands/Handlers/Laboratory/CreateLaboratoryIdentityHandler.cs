using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Domain.DataTransferObjects;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;
using XFramework.Domain.Generic.Enums;
using XFramework.Integration.Services.Helpers;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class CreateLaboratoryIdentityHandler : CommandBaseHandler, IRequestHandler<CreateLaboratoryIdentityCmd, CmdResponse<CreateLaboratoryIdentityCmd>>
{
    public CreateLaboratoryIdentityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLaboratoryIdentityCmd>> Handle(CreateLaboratoryIdentityCmd request, CancellationToken cancellationToken)
    {
        var laboratory = await _dataLayer.HealthEssentialsContext.Laboratories
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.LaboratoryGuid}", cancellationToken: cancellationToken);
       
        if (laboratory is null)
        {
            return new ()
            {
                Message = $"Laboratory with Guid {request.CredentialGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
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
            var address = await _dataLayer.XnelSystemsContext.IdentityAddresses.FirstOrDefaultAsync(i => i.AddressEntities.Name == $"Office", cancellationToken: cancellationToken);
            var addressEntity = await _dataLayer.XnelSystemsContext.IdentityAddressEntities.FirstOrDefaultAsync(i => i.Name == $"Office", cancellationToken: cancellationToken);

            var country = await _dataLayer.XnelSystemsContext.AddressCountries.FirstOrDefaultAsync(i => i.Guid == $"{request.Address.CountryGuid}", cancellationToken: cancellationToken);
            var region = await _dataLayer.XnelSystemsContext.AddressRegions.FirstOrDefaultAsync(i => i.Guid == $"{request.Address.RegionGuid}", cancellationToken: cancellationToken);
            var province = await _dataLayer.XnelSystemsContext.AddressProvinces.FirstOrDefaultAsync(i => i.Guid == $"{request.Address.ProvinceGuid}", cancellationToken: cancellationToken);
            var city = await _dataLayer.XnelSystemsContext.AddressCities.FirstOrDefaultAsync(i => i.Guid == $"{request.Address.CityGuid}", cancellationToken: cancellationToken);
            var barangay = await _dataLayer.XnelSystemsContext.AddressBarangays.FirstOrDefaultAsync(i => i.Guid == $"{request.Address.BarangayGuid}", cancellationToken: cancellationToken);
            
            if (address is null)
            {
                await _dataLayer.XnelSystemsContext.IdentityAddresses.AddAsync(new()
                {
                    IdentityInfoId = credential.IdentityInfoId,
                    Barangay = barangay?.Id,
                    City = city?.Id,
                    Region = region?.Id,
                    AddressEntitiesId = addressEntity?.Id,
                    DefaultAddress = true,
                    Province = province?.Id,
                    Country = country?.Id,
                    Guid = $"{Guid.NewGuid()}",
                }, CancellationToken.None);
            }
        }

        /*var contactGroup = await _dataLayer.XnelSystemsContext.IdentityContactGroups.AsNoTracking().FirstOrDefaultAsync(i => i.Name == "WORK_LABORATORY", cancellationToken: CancellationToken.None);
        var emailContactType = await _dataLayer.XnelSystemsContext.IdentityContactEntities.AsNoTracking().FirstOrDefaultAsync(i => i.Name == "Email", cancellationToken: CancellationToken.None);
        var phoneContactType = await _dataLayer.XnelSystemsContext.IdentityContactEntities.AsNoTracking().FirstOrDefaultAsync(i => i.Name == "Phone", cancellationToken: CancellationToken.None);

        
        var workEmail = new IdentityContact
        {
            EntityId = emailContactType.Id,
            Value = request.Email,
            UserCredentialId = credential.Id,
            Guid = $"{Guid.NewGuid()}",
            GroupId = contactGroup.Id,
        };

        var workPhone = new IdentityContact
        {
            EntityId = phoneContactType.Id,
            Value = request.PhoneNumber.ValidatePhoneNumber(convertOnly: true),
            UserCredentialId = credential.Id,
            Guid = $"{Guid.NewGuid()}",
            GroupId = contactGroup.Id,
        };*/
        
        var entity = new LaboratoryMember
        {
            CredentialId = credential.Id,
            LaboratoryId = laboratory.Id,
            Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}",
            Name = request.ProfessionalName,
            Status = (int) GenericStatusType.Pending
        };

        /*await _dataLayer.XnelSystemsContext.IdentityContacts.AddAsync(workEmail, CancellationToken.None);
        await _dataLayer.XnelSystemsContext.IdentityContacts.AddAsync(workPhone, CancellationToken.None);*/
        
        await _dataLayer.HealthEssentialsContext.LaboratoryMembers.AddAsync(entity, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Laboratory member with Guid {entity.Guid} has been created",
            HttpStatusCode = HttpStatusCode.Accepted,
            Request = new()
            {
                Guid = Guid.Parse(entity.Guid)
            }
        };
    }
}