using HealthEssentials.Core.DataAccess.Commands.Entity.Ailment;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Ailment;

public class DeleteAilmentTagHandler : CommandBaseHandler, IRequestHandler<DeleteAilmentTagCmd, CmdResponse<DeleteAilmentTagCmd>>
{
    public DeleteAilmentTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteAilmentTagCmd>> Handle(DeleteAilmentTagCmd request, CancellationToken cancellationToken)
    {
        var existingAilmentTag = await _dataLayer.HealthEssentialsContext.AilmentTags
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (existingAilmentTag is null)
        {
            return new ()
            {
                Message = $"Ailment Tag with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingAilmentTag.IsDeleted = true;
        existingAilmentTag.IsEnabled = false;
        
        _dataLayer.HealthEssentialsContext.Update(existingAilmentTag);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Ailment Tag with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}