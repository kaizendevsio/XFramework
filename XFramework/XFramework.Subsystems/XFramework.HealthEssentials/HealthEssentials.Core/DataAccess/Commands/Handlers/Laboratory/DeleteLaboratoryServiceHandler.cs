using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryServiceHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryServiceCmd, CmdResponse<DeleteLaboratoryServiceCmd>>
{
    public DeleteLaboratoryServiceHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<CmdResponse<DeleteLaboratoryServiceCmd>> Handle(DeleteLaboratoryServiceCmd request, CancellationToken cancellationToken)
    {
        var existingLaboratoryService = await _dataLayer.HealthEssentialsContext.LaboratoryServices.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingLaboratoryService == null)
        {
            return new()
            {
                Message = $"Laboratory Service with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingLaboratoryService.IsDeleted = true;
        existingLaboratoryService.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingLaboratoryService);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Laboratory Service with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}