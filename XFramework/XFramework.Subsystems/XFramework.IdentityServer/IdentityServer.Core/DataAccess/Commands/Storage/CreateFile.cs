using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ByteSizeLib;
using XFramework.Core.Services;

namespace IdentityServer.Core.DataAccess.Commands.Storage;

public class CreateFile(
        AppDbContext appDbContext,
        ILogger<CreateFile> logger,
        ITenantService tenantService,
        IRequestHandler<Create<StorageFile>, CmdResponse<StorageFile>> baseHandler
    ) 
    : ICreateHandler<StorageFile>, IDecorator
{
    public async Task<CmdResponse<StorageFile>> Handle(Create<StorageFile> request, CancellationToken cancellationToken)
    {
        var storageFileType = await appDbContext.StorageFileTypes.FirstOrDefaultAsync(i => i.Id == request.Model.TypeId, cancellationToken);
        if (storageFileType == null)
        {
            return new ()
            {
                Message = $"File type with id {request.Model.TypeId} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
     
        var fileIdentifier = await appDbContext.StorageFileIdentifiers.FirstOrDefaultAsync(i => i.Id == request.Model.StorageFileIdentifierId, cancellationToken);
        if (fileIdentifier == null)
        {
            return new ()
            {
                Message = $"File identifier with id {request.Model.StorageFileIdentifierId} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        // Upload Files to azure blob storage
        var connectionConfig =  await appDbContext.RegistryConfigurationGroups
            .Include(i => i.RegistryConfigurations)
            .FirstOrDefaultAsync(i => i.Name == "AzureBlobStorage", CancellationToken.None);

        var blobServiceClient = new BlobServiceClient(connectionConfig.RegistryConfigurations.FirstOrDefault(i => i.Key == "ConnectionString").Value);

        var client = blobServiceClient.GetBlobContainerClient(request.Model.BlobContainer);
        var blob = client.GetBlobClient(request.Model.ContentPath.Replace($"{request.Model.BlobContainer}/", ""));
        await blob.UploadAsync(BinaryData.FromBytes(request.Model.FileBytes), new BlobUploadOptions {HttpHeaders = new() {ContentType = request.Model.ContentType}}, CancellationToken.None);

        var file = request.Adapt<StorageFile>();
        file.Type = storageFileType;
        file.StorageFileIdentifier = fileIdentifier;
        file.FileSize = (decimal?) ByteSize.FromBytes(request.Model.FileBytes.Length).KiloBytes;
        
        await baseHandler.Handle(new Create<StorageFile>(file), cancellationToken);
        
        //await appDbContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Response = request.Model,
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}