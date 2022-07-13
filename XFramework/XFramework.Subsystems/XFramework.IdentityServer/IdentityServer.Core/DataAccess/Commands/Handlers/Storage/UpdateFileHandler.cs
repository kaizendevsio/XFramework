using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ByteSizeLib;
using IdentityServer.Core.DataAccess.Commands.Entity.Storage;
using XFramework.Integration.Interfaces;

namespace IdentityServer.Core.DataAccess.Commands.Handlers.Storage;

public class UpdateFileHandler : CommandBaseHandler, IRequestHandler<UpdateFileCmd,CmdResponse<UpdateFileCmd>>
{
    private readonly IHelperService _helperService;

    public UpdateFileHandler(IDataLayer dataLayer, IHelperService helperService)
    {
        _helperService = helperService;
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<UpdateFileCmd>> Handle(UpdateFileCmd request, CancellationToken cancellationToken)
    {
        var existingRecord = await _dataLayer.StorageFiles.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken);
        if (existingRecord == null)
        {
            return new ()
            {
                Message = $"File with guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var storageFileEntity = await _dataLayer.StorageFileEntities.FirstOrDefaultAsync(i => i.Guid == $"{request.EntityGuid}", cancellationToken);
        if (storageFileEntity == null)
        {
            return new ()
            {
                Message = $"File with guid {request.EntityGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
     
        var fileIdentifier = await _dataLayer.StorageFileIdentifiers.FirstOrDefaultAsync(i => i.Guid == $"{request.StorageFileIdentifierGuid}", cancellationToken);
        if (fileIdentifier == null)
        {
            return new ()
            {
                Message = $"File identifier with guid {request.StorageFileIdentifierGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        // Upload Files to azure blob storage
        var connectionConfig =  await _dataLayer.RegistryConfigurationGroups
            .Include(i => i.RegistryConfigurations)
            .FirstOrDefaultAsync(i => i.Name == "AzureBlobStorage", CancellationToken.None);

        var blobServiceClient = new BlobServiceClient(connectionConfig.RegistryConfigurations.FirstOrDefault(i => i.Key == "ConnectionString").Value);

        var client = blobServiceClient.GetBlobContainerClient("files-kyc");
        var blob = client.GetBlobClient(request.ContentPath);
        await blob.UploadAsync(BinaryData.FromBytes(request.FileBytes), new BlobUploadOptions {HttpHeaders = new() {ContentType = request.ContentType}}, CancellationToken.None);

        var file = request.Adapt(existingRecord);
        file.Entity = storageFileEntity;
        file.StorageFileIdentifier = fileIdentifier;
        file.FileSize = (decimal?) ByteSize.FromBytes(request.FileBytes.Length).KiloBytes;
        
        await _dataLayer.StorageFiles.AddAsync(file, cancellationToken);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);

        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }

}