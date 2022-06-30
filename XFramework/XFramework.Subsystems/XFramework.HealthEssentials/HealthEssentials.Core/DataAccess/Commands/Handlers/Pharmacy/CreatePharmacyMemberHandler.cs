using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;
using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;
using HealthEssentials.Domain.DataTransferObjects;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;
using XFramework.Domain.Generic.Enums;
using XFramework.Integration.Services.Helpers;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class CreatePharmacyMemberHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyMemberCmd, CmdResponse<CreatePharmacyMemberCmd>>
{
    public CreatePharmacyMemberHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreatePharmacyMemberCmd>> Handle(CreatePharmacyMemberCmd request, CancellationToken cancellationToken)
    {
        var pharmacy = await _dataLayer.HealthEssentialsContext.Pharmacies
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.PharmacyGuid}", cancellationToken: cancellationToken);
       
        if (pharmacy is null)
        {
            return new ()
            {
                Message = $"Pharmacy with Guid {request.PharmacyGuid} does not exist",
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

        /*var contactGroup = await _dataLayer.XnelSystemsContext.IdentityContactGroups.AsNoTracking().FirstOrDefaultAsync(i => i.Name == "WORK_PHARMACY", cancellationToken: CancellationToken.None);
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
        

        var entity = new PharmacyMember
        {
            CredentialId = credential.Id,
            PharmacyId = pharmacy.Id,
            Guid = $"{Guid.NewGuid()}",
            Name = request.ProfessionalName,
            Status = (int) GenericStatusType.Pending
        };

        /*await _dataLayer.XnelSystemsContext.IdentityContacts.AddAsync(workEmail, CancellationToken.None);
        await _dataLayer.XnelSystemsContext.IdentityContacts.AddAsync(workPhone, CancellationToken.None);*/
        await _dataLayer.HealthEssentialsContext.PharmacyMembers.AddAsync(entity, CancellationToken.None);

        await _dataLayer.XnelSystemsContext.SaveChangesAsync(CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Consultation with Guid {entity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(entity.Guid)
            }
        };
    }
}