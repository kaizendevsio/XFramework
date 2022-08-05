using HealthEssentials.Core.DataAccess.Commands.Entity.MetaData;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.MetaData;

public class DeleteMetaDataEntityGroupHandler : CommandBaseHandler, IRequestHandler<DeleteMetaDataEntityGroupCmd, CmdResponse<DeleteMetaDataEntityGroupCmd>>
{
    public DeleteMetaDataEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteMetaDataEntityGroupCmd>> Handle(DeleteMetaDataEntityGroupCmd request, CancellationToken cancellationToken)
    {
        var existingGroup = await _dataLayer.HealthEssentialsContext.MetaDataEntityGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingGroup is null)
        {
            return new ()
            {
                Message = $"Meta Data Entity Group with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingGroup.IsDeleted = true;
        existingGroup.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingGroup);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Meta Data Entity Group with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}