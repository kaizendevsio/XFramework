using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ByteSizeLib;
using IdentityServer.Core.DataAccess.Commands.Entity.Storage;
using XFramework.Integration.Interfaces;

namespace IdentityServer.Core.DataAccess.Commands.Handlers.Storage;

public class CreateFileHandler : CommandBaseHandler, IRequestHandler<CreateFileCmd,CmdResponse<CreateFileCmd>>
{
    private readonly IHelperService _helperService;

    public CreateFileHandler(IDataLayer dataLayer, IHelperService helperService)
    {
        _helperService = helperService;
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<CreateFileCmd>> Handle(CreateFileCmd request, CancellationToken cancellationToken)
    {
        var storageFileEntity = await _dataLayer.StorageFileEntities.FirstOrDefaultAsync(i => i.Guid == $"{request.EntityGuid}", cancellationToken);
        if (storageFileEntity == null)
        {
            return new ()
            {
                Message = $"File entity with guid {request.EntityGuid} not found",
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

        var client = blobServiceClient.GetBlobContainerClient(request.BlobContainer);
        var blob = client.GetBlobClient(request.ContentPath);
        await blob.UploadAsync(BinaryData.FromBytes(request.FileBytes), new BlobUploadOptions {HttpHeaders = new() {ContentType = request.ContentType}}, CancellationToken.None);

        var file = request.Adapt<StorageFile>();
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