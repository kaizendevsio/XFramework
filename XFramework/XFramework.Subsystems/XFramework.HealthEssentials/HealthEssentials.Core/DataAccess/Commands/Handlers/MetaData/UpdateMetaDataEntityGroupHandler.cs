using HealthEssentials.Core.DataAccess.Commands.Entity.MetaData;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.MetaData;

public class UpdateMetaDataEntityGroupHandler : CommandBaseHandler, IRequestHandler<UpdateMetaDataEntityGroupCmd, CmdResponse<UpdateMetaDataEntityGroupCmd>>
{
    public UpdateMetaDataEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateMetaDataEntityGroupCmd>> Handle(UpdateMetaDataEntityGroupCmd request, CancellationToken cancellationToken)
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
        var updatedGroup = request.Adapt(existingGroup);

        _dataLayer.HealthEssentialsContext.Update(updatedGroup);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Meta Data Entity Group with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}