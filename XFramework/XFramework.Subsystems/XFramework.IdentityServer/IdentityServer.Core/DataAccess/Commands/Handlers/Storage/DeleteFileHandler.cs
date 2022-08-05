using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using ByteSizeLib;
using IdentityServer.Core.DataAccess.Commands.Entity.Storage;
using XFramework.Integration.Interfaces;

namespace IdentityServer.Core.DataAccess.Commands.Handlers.Storage;

public class DeleteFileHandler : CommandBaseHandler, IRequestHandler<DeleteFileCmd,CmdResponse<DeleteFileCmd>>
{
    private readonly IHelperService _helperService;

    public DeleteFileHandler(IDataLayer dataLayer, IHelperService helperService)
    {
        _helperService = helperService;
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<DeleteFileCmd>> Handle(DeleteFileCmd request, CancellationToken cancellationToken)
    {
        var existingRecord = await _dataLayer.StorageFiles.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken);
        if (existingRecord == null)
        {
            return new()
            {
                Message = $"File with guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingRecord.IsDeleted = true;
        existingRecord.IsEnabled = false;
        
        _dataLayer.StorageFiles.Update(existingRecord);
        await _dataLayer.SaveChangesAsync(CancellationToken.None);

        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}