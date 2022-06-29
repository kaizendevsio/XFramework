using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using HealthEssentials.Core.DataAccess.Commands.Entity.Logistic;
using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;
using XFramework.Domain.Generic.Enums;
using XFramework.Integration.Interfaces;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class CreatePharmacyHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyCmd, CmdResponse<CreatePharmacyCmd>>
{
    private readonly IHelperService _helperService;

    public CreatePharmacyHandler(IDataLayer dataLayer, IHelperService helperService)
    {
        _helperService = helperService;
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreatePharmacyCmd>> Handle(CreatePharmacyCmd request, CancellationToken cancellationToken)
    {
        var entity = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.Pharmacy>();
        entity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        entity.EntityId = 1;
        entity.Status = (int) GenericStatusType.Pending;

       
        var country = await _dataLayer.XnelSystemsContext.AddressCountries.FirstOrDefaultAsync(i => i.Guid == $"{request.Address.CountryGuid}", cancellationToken: cancellationToken);
        var region = await _dataLayer.XnelSystemsContext.AddressRegions.FirstOrDefaultAsync(i => i.Guid == $"{request.Address.RegionGuid}", cancellationToken: cancellationToken);
        var province = await _dataLayer.XnelSystemsContext.AddressProvinces.FirstOrDefaultAsync(i => i.Guid == $"{request.Address.ProvinceGuid}", cancellationToken: cancellationToken);
        var city = await _dataLayer.XnelSystemsContext.AddressCities.FirstOrDefaultAsync(i => i.Guid == $"{request.Address.CityGuid}", cancellationToken: cancellationToken);
        var barangay = await _dataLayer.XnelSystemsContext.AddressBarangays.FirstOrDefaultAsync(i => i.Guid == $"{request.Address.BarangayGuid}", cancellationToken: cancellationToken);

        var location = new PharmacyLocation()
        {
            Name = request.Address.Name,
            UnitNumber = request.Address.UnitNumber,
            BarangayId = barangay.Id,
            CityId = city.Id,
            RegionId = region.Id,
            MainAddress = true,
            ProvinceId = province.Id,
            CountryId = country.Id,
            Pharmacy = entity,
            Status = (int) GenericStatusType.Pending
        };

        await _dataLayer.HealthEssentialsContext.PharmacyLocations.AddAsync(location, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.Pharmacies.AddAsync(entity, CancellationToken.None);
        
        var tags = await _dataLayer.HealthEssentialsContext.Tags.Where(i => i.EntityId == 1).ToListAsync(CancellationToken.None);
        foreach (var item in request.TagList)
        {
            var tag = tags.FirstOrDefault(i => i.Name == item.Tag.ToString());
            await _dataLayer.HealthEssentialsContext.PharmacyTags.AddAsync(new()
            {
                Pharmacy = entity,
                TagId = tag?.Id,
                Value = item.IsEnabled.ToString()
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
            var filePath = $"{location.Guid}-pharmacy-{_helperService.GenerateRandomString(8)}-{fileUploadRequest.FileName}";
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
            Message = "Successfully created pharmacy",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(entity.Guid)
            }
        };
    }
}