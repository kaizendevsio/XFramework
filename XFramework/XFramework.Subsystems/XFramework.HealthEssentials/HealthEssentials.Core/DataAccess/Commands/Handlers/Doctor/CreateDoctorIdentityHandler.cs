using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;
using HealthEssentials.Domain.DataTransferObjects;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;
using XFramework.Domain.Generic.Enums;
using XFramework.Integration.Interfaces;
using XFramework.Integration.Services.Helpers;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Doctor;

public class CreateDoctorIdentityHandler : CommandBaseHandler, IRequestHandler<CreateDoctorIdentityCmd, CmdResponse<CreateDoctorIdentityCmd>>
{
    private readonly IHelperService _helperService;

    public CreateDoctorIdentityHandler(IDataLayer dataLayer, IHelperService helperService)
    {
        _helperService = helperService;
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

        /*var contactGroup = await _dataLayer.XnelSystemsContext.IdentityContactGroups.AsNoTracking().FirstOrDefaultAsync(i => i.Name == "WORK_DOCTOR", cancellationToken: CancellationToken.None);
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
        
        var doctor = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.Doctor>();
        doctor.CredentialId = credential.Id;
        doctor.Name = request.ProfessionalName;
        doctor.Guid = $"{Guid.NewGuid()}";
        doctor.EntityId = 1;
        doctor.Status = (int) GenericStatusType.Pending;

        foreach (var consultationGuid in request.SupportedConsultationList)
        {
            var consultation = await _dataLayer.HealthEssentialsContext.Consultations.FirstOrDefaultAsync(i => i.Guid == $"{consultationGuid}", CancellationToken.None);
            if (consultation is null) continue;
            await _dataLayer.HealthEssentialsContext.DoctorConsultations.AddAsync(new()
            {
                Quantity = 1,
                Consultation = consultation,
                Doctor = doctor
            }, CancellationToken.None);
        }
        
        // Upload Files to azure blob storage
        var connectionConfig =  await _dataLayer.XnelSystemsContext.RegistryConfigurationGroups
            .Include(i => i.RegistryConfigurations)
            .FirstOrDefaultAsync(i => i.Name == "AzureBlobStorage", CancellationToken.None);

        var storageFileIdentifiers = await _dataLayer.XnelSystemsContext.StorageFileIdentifiers.ToListAsync(CancellationToken.None);
        var blobServiceClient = new BlobServiceClient(connectionConfig.RegistryConfigurations.FirstOrDefault(i => i.Key == "ConnectionString").Value);

        foreach (var fileUploadRequest in request.FileList)
        {
            var filePath = $"{doctor.Guid}-doctor-{_helperService.GenerateRandomString(8)}-{fileUploadRequest.FileName}";
            var client = blobServiceClient.GetBlobContainerClient("files-kyc");
            var blob = client.GetBlobClient(filePath);
            await blob.UploadAsync(BinaryData.FromBytes(fileUploadRequest.FileBytes), new BlobUploadOptions {HttpHeaders = new() {ContentType = fileUploadRequest.ContentType}}, CancellationToken.None);
            
            await _dataLayer.XnelSystemsContext.StorageFiles.AddAsync(new()
            {
                ContentPath = $"/files-kyc/{filePath}",
                IdentifierGuid = doctor.Guid,
                StorageFileIdentifierId = storageFileIdentifiers.FirstOrDefault(i => i.Guid == $"{fileUploadRequest.Entity}")?.Id,
                EntityId = 1
            });
        }
        
        /*await _dataLayer.XnelSystemsContext.IdentityContacts.AddAsync(workEmail, CancellationToken.None);
        await _dataLayer.XnelSystemsContext.IdentityContacts.AddAsync(workPhone, CancellationToken.None);*/
        
        await _dataLayer.HealthEssentialsContext.Doctors.AddAsync(doctor, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        await _dataLayer.XnelSystemsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Doctor {request.ProfessionalName} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            Request = new()
            {
                Guid = Guid.Parse(doctor.Guid)
            }
        };
    }
}