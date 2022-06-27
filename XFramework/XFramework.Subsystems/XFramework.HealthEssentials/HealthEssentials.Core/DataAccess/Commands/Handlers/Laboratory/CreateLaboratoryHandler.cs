using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;
using XFramework.Domain.Generic.Enums;
using XFramework.Integration.Interfaces;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class CreateLaboratoryHandler : CommandBaseHandler, IRequestHandler<CreateLaboratoryCmd, CmdResponse<CreateLaboratoryCmd>>
{
    private readonly IHelperService _helperService;

    public CreateLaboratoryHandler(IDataLayer dataLayer, IHelperService helperService)
    {
        _helperService = helperService;
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLaboratoryCmd>> Handle(CreateLaboratoryCmd request, CancellationToken cancellationToken)
    {
        var entity = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.Laboratory>();
        entity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        entity.EntityId = 1;
        entity.Status = (int) GenericStatusType.Pending;

        var country = await _dataLayer.XnelSystemsContext.AddressCountries.FirstOrDefaultAsync(i => i.Guid == $"{request.Address.CountryGuid}", cancellationToken: cancellationToken);
        var region = await _dataLayer.XnelSystemsContext.AddressRegions.FirstOrDefaultAsync(i => i.Guid == $"{request.Address.RegionGuid}", cancellationToken: cancellationToken);
        var province = await _dataLayer.XnelSystemsContext.AddressProvinces.FirstOrDefaultAsync(i => i.Guid == $"{request.Address.ProvinceGuid}", cancellationToken: cancellationToken);
        var city = await _dataLayer.XnelSystemsContext.AddressCities.FirstOrDefaultAsync(i => i.Guid == $"{request.Address.CityGuid}", cancellationToken: cancellationToken);
        var barangay = await _dataLayer.XnelSystemsContext.AddressBarangays.FirstOrDefaultAsync(i => i.Guid == $"{request.Address.BarangayGuid}", cancellationToken: cancellationToken);

        var location = new LaboratoryLocation
        {
            Name = request.Address.Name,
            UnitNumber = request.Address.UnitNumber,
            Barangay = barangay.Id,
            City = city.Id,
            Region = region.Id,
            MainAddress = true,
            Province = province.Id,
            Country = country.Id,
            Laboratory = entity,
            Status = (int) GenericStatusType.Pending
        };
        await _dataLayer.HealthEssentialsContext.Laboratories.AddAsync(entity, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.LaboratoryLocations.AddAsync(location, CancellationToken.None);
        
        foreach (var consultationGuid in request.SupportedLaboratoryServiceList)
        {
            var serviceEntity = await _dataLayer.HealthEssentialsContext.LaboratoryServiceEntities.FirstOrDefaultAsync(i => i.Guid == $"{consultationGuid}", CancellationToken.None);
            if (serviceEntity is null) continue;
            await _dataLayer.HealthEssentialsContext.LaboratoryServices.AddAsync(new()
            {
                Name = serviceEntity.Name,
                Description = serviceEntity.Description,
                Quantity = 1,
                Entity = serviceEntity,
                Laboratory = entity,
                LaboratoryLocation = location
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
            var filePath = $"{location.Guid}-laboratory-{_helperService.GenerateRandomString(8)}-{fileUploadRequest.FileName}";
            var client = blobServiceClient.GetBlobContainerClient("files-kyc");
            var blob = client.GetBlobClient(filePath);
            await blob.UploadAsync(BinaryData.FromBytes(fileUploadRequest.FileBytes), new BlobUploadOptions {HttpHeaders = new() {ContentType = fileUploadRequest.ContentType}}, CancellationToken.None);
            
            await _dataLayer.XnelSystemsContext.StorageFiles.AddAsync(new()
            {
                ContentPath = $"/files-kyc/{filePath}",
                IdentifierGuid = location.Guid,
                StorageFileIdentifierId = storageFileIdentifiers.FirstOrDefault(i => i.Guid == $"{fileUploadRequest.Entity}")?.Id,
                EntityId = 1
            });
        }
        
        await _dataLayer.XnelSystemsContext.SaveChangesAsync(CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Laboratory with Guid {entity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(entity.Guid)
            }
        };
    }
}