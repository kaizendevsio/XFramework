using HealthEssentials.Core.DataAccess.Commands.Entity.Tag;
using TypeSupport.Extensions;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Tag;

public class DeleteTagEntityGroupHandler : CommandBaseHandler, IRequestHandler<DeleteTagEntityGroupCmd, CmdResponse<DeleteTagEntityGroupCmd>>
{
    public DeleteTagEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteTagEntityGroupCmd>> Handle(DeleteTagEntityGroupCmd request, CancellationToken cancellationToken)
    {
        var existingGroup = await _dataLayer.HealthEssentialsContext.TagEntityGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingGroup == null)
        {
            return new()
            {
                Message = $"Tag Entity Group with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingGroup.IsDeleted = true;
        existingGroup.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingGroup); 
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(cancellationToken);
        
        return new()
        {
            Message = $"Tag Entity Group with Guid {request.Guid} deleted successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
        
    }
}