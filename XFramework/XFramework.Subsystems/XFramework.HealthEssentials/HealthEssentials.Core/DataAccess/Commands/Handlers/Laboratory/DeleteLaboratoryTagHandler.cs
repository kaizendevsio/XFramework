using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryTagHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryTagCmd, CmdResponse<DeleteLaboratoryTagCmd>>
{
    public DeleteLaboratoryTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteLaboratoryTagCmd>> Handle(DeleteLaboratoryTagCmd request, CancellationToken cancellationToken)
    {
        var existingTag = await _dataLayer.HealthEssentialsContext.LaboratoryTags.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingTag is null)
        {
            return new ()
            {
                Message = $"Laboratory Tag with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingTag.IsDeleted = true;
        existingTag.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingTag);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Laboratory Tag with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}