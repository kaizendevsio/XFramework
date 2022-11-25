using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryServiceTagHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryServiceTagCmd, CmdResponse<DeleteLaboratoryServiceTagCmd>>
{
    public DeleteLaboratoryServiceTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteLaboratoryServiceTagCmd>> Handle(DeleteLaboratoryServiceTagCmd request, CancellationToken cancellationToken)
    {
        var existingServiceTag = await _dataLayer.HealthEssentialsContext.LaboratoryServiceTags.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingServiceTag is null)
        {
            return new ()
            {
                Message = $"Laboratory Service Tag with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingServiceTag.IsDeleted = true;
        existingServiceTag.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingServiceTag);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Laboratory Service Tag with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}