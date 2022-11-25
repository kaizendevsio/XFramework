using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryLocationTagHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryLocationTagCmd, CmdResponse<DeleteLaboratoryLocationTagCmd>>
{
    public DeleteLaboratoryLocationTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteLaboratoryLocationTagCmd>> Handle(DeleteLaboratoryLocationTagCmd request, CancellationToken cancellationToken)
    {
        var existingLocationTag = await _dataLayer.HealthEssentialsContext.LaboratoryLocationTags.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingLocationTag is null)
        {
            return new ()
            {
                Message = $"Laboratory Location Tag with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingLocationTag.IsDeleted = true;
        existingLocationTag.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingLocationTag);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Laboratory Location Tag with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}