using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ByteSizeLib;
using XFramework.Core.Services;

namespace IdentityServer.Core.DataAccess.Commands.Storage;

public class CreateFile(
        DbContext dbContext,
        ILogger<CreateFile> logger,
        ITenantService tenantService,
        IRequestHandler<Create<StorageFile>, CmdResponse<StorageFile>> baseHandler
    ) 
    : ICreateHandler<StorageFile>, IDecorator
{
    public async Task<CmdResponse<StorageFile>> Handle(Create<StorageFile> request, CancellationToken cancellationToken)
    {
        var storageFileType = await dbContext.Set<StorageFileType>().FirstOrDefaultAsync(i => i.Id == request.Model.TypeId, cancellationToken);
        if (storageFileType == null)
        {
            return new ()
            {
                Message = $"File type with id {request.Model.TypeId} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
     
        var fileIdentifier = await dbContext.Set<StorageFileIdentifier>().FirstOrDefaultAsync(i => i.Id == request.Model.StorageFileIdentifierId, cancellationToken);
        if (fileIdentifier == null)
        {
            return new ()
            {
                Message = $"File identifier with id {request.Model.StorageFileIdentifierId} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        // Upload Files to azure blob storage
        var connectionConfig = await dbContext.Set<RegistryConfigurationGroup>()
            .Include(i => i.RegistryConfigurations)
            .FirstOrDefaultAsync(i => i.Name == "AzureBlobStorage", CancellationToken.None);

        var blobServiceClient = new BlobServiceClient(connectionConfig.RegistryConfigurations.FirstOrDefault(i => i.Key == "ConnectionString").Value);

        var client = blobServiceClient.GetBlobContainerClient(request.Model.BlobContainer);
        var blob = client.GetBlobClient(request.Model.ContentPath.Replace($"{request.Model.BlobContainer}/", ""));
        await blob.UploadAsync(BinaryData.FromBytes(request.Model.FileBytes), new BlobUploadOptions {HttpHeaders = new() {ContentType = request.Model.ContentType}}, CancellationToken.None);

        request.Model.Type = storageFileType;
        request.Model.StorageFileIdentifier = fileIdentifier;
        request.Model.FileSize = (decimal?) ByteSize.FromBytes(request.Model.FileBytes.Length).KiloBytes;
        
        var response = await baseHandler.Handle(request, cancellationToken);
        return response;
    }
}