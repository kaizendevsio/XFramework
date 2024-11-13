using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ByteSizeLib;
using XFramework.Core.Services;
using XFramework.Integration.Abstractions;

namespace IdentityServer.Core.Commands.Storage;

public class CreateFile(
        DbContext dbContext,
        ILogger<CreateFile> logger,
        ITenantService tenantService,
        IHelperService helperService,
        IRequestHandler<Create<StorageFile>, CmdResponse<StorageFile>> baseHandler
    ) 
    : ICreateHandler<StorageFile>, IDecorator
{
    public async Task<CmdResponse<StorageFile>> Handle(Create<StorageFile> request, CancellationToken cancellationToken)
    {
        if (request.Model.FileBytes is null)
        {
            return new ()
            {
                Message = "Cannot upload empty file",
                HttpStatusCode = HttpStatusCode.BadRequest
            };
        }
        
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
            .Where(i => i.Name == "AzureBlobStorage")
            .Where(i => i.TenantId == request.Metadata.TenantId)
            .FirstOrDefaultAsync(CancellationToken.None);

        var connectionString = connectionConfig?.RegistryConfigurations
            .FirstOrDefault(i => i.Key == "ConnectionString")?.Value;

        if (string.IsNullOrEmpty(connectionString))
        {
            return new ()
            {
                Message = $"Azure blob storage connection string not found",
                HttpStatusCode = HttpStatusCode.InternalServerError
            };
        }
        var blobServiceClient = new BlobServiceClient(connectionString);

        var client = blobServiceClient.GetBlobContainerClient(request.Model.BlobContainer);
        var blob = client.GetBlobClient(request.Model.ContentPath.Replace($"{request.Model.BlobContainer}/", ""));
        await blob.UploadAsync(
            content: BinaryData.FromBytes(request.Model.FileBytes),
            options: new BlobUploadOptions
            {
                HttpHeaders = new()
                {
                    ContentType = request.Model.ContentType
                }
            }, 
            cancellationToken: CancellationToken.None);

        request.Model.Type = storageFileType;
        request.Model.StorageFileIdentifier = fileIdentifier;
        request.Model.FileSize = (decimal?) ByteSize.FromBytes(request.Model.FileBytes.Length).KiloBytes;
        
        return await baseHandler.Handle(request, cancellationToken);
    }
}