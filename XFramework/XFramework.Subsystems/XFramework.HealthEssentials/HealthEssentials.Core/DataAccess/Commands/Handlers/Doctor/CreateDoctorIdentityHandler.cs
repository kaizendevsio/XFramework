using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;
using HealthEssentials.Core.Interfaces;
using HealthEssentials.Domain.DataTransferObjects;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;
using Mapster;
using Microsoft.EntityFrameworkCore;
using XFramework.Integration.Services.Helpers;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Doctor;

public class CreateDoctorIdentityHandler : CommandBaseHandler, IRequestHandler<CreateDoctorIdentityCmd, CmdResponse<CreateDoctorIdentityCmd>>
{
    public CreateDoctorIdentityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateDoctorIdentityCmd>> Handle(CreateDoctorIdentityCmd request, CancellationToken cancellationToken)
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

        var specialty = await _dataLayer.HealthEssentialsContext.DoctorEntities
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Name == $"{request.Specialty}", cancellationToken: cancellationToken);
       
        if (specialty is null)
        {
            var specialtyEntry = await _dataLayer.HealthEssentialsContext.DoctorEntities.AddAsync(new DoctorEntity
            {
                Name = $"{request.Specialty}",
                GroupId = 1
            }, CancellationToken.None);
            specialty = specialtyEntry.Entity;
        }

        if (request.Address is not null)
        {
            var country = _dataLayer.XnelSystemsContext.AddressCountries.FirstOrDefaultAsync(i => i.Guid == $"{request.Address.CountryGuid}", cancellationToken: cancellationToken);
            var region = _dataLayer.XnelSystemsContext.AddressRegions.FirstOrDefaultAsync(i => i.Guid == $"{request.Address.RegionGuid}", cancellationToken: cancellationToken);
            var province = _dataLayer.XnelSystemsContext.AddressProvinces.FirstOrDefaultAsync(i => i.Guid == $"{request.Address.ProvinceGuid}", cancellationToken: cancellationToken);
            var city = _dataLayer.XnelSystemsContext.AddressCities.FirstOrDefaultAsync(i => i.Guid == $"{request.Address.CityGuid}", cancellationToken: cancellationToken);
            var barangay = _dataLayer.XnelSystemsContext.AddressBarangays.FirstOrDefaultAsync(i => i.Guid == $"{request.Address.BarangayGuid}", cancellationToken: cancellationToken);
            
            await Task.WhenAll(country, region, province, city, barangay);
        }

        var contactGroup = _dataLayer.XnelSystemsContext.IdentityContactGroups.AsNoTracking().FirstOrDefaultAsync(i => i.Name == "WORK_DOCTOR", cancellationToken: CancellationToken.None);
        var emailContactType = _dataLayer.XnelSystemsContext.IdentityContactEntities.AsNoTracking().FirstOrDefaultAsync(i => i.Name == "Email", cancellationToken: CancellationToken.None);
        var phoneContactType = _dataLayer.XnelSystemsContext.IdentityContactEntities.AsNoTracking().FirstOrDefaultAsync(i => i.Name == "Phone", cancellationToken: CancellationToken.None);

        await Task.WhenAll(contactGroup, emailContactType, phoneContactType);
        
        var workEmail = new IdentityContact
        {
            EntityId = emailContactType.Result.Id,
            Value = request.Email,
            UserCredentialId = credential.Id,
            Guid = $"{Guid.NewGuid()}",
            GroupId = contactGroup.Result.Id,
        };

        var workPhone = new IdentityContact
        {
            EntityId = phoneContactType.Result.Id,
            Value = request.PhoneNumber.ValidatePhoneNumber(convertOnly: true),
            UserCredentialId = credential.Id,
            Guid = $"{Guid.NewGuid()}",
            GroupId = contactGroup.Result.Id,
        };
        
        var entity = new Domain.DataTransferObjects.XnelSystemsHealthEssentials.Doctor
        {
            CredentialId = credential.Id,
            Description = request.Description,
            Guid = $"{Guid.NewGuid()}",
            Name = request.ProfessionalName,
            Entity = specialty
        };
        
        await _dataLayer.XnelSystemsContext.IdentityContacts.AddAsync(workEmail, CancellationToken.None);
        await _dataLayer.XnelSystemsContext.IdentityContacts.AddAsync(workPhone, CancellationToken.None);
        
        await _dataLayer.HealthEssentialsContext.Doctors.AddAsync(entity, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Doctor {request.ProfessionalName} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            Request = new()
            {
                Guid = Guid.Parse(entity.Guid)
            }
        };
    }
}