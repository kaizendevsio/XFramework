using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class DeleteLaboratoryServiceEntityHandler : CommandBaseHandler, IRequestHandler<DeleteLaboratoryServiceEntityCmd, CmdResponse<DeleteLaboratoryServiceEntityCmd>>
{
    public DeleteLaboratoryServiceEntityHandler()
    {
        
    }
    public async Task<CmdResponse<DeleteLaboratoryServiceEntityCmd>> Handle(DeleteLaboratoryServiceEntityCmd request, CancellationToken cancellationToken)
    {
        var existingRecord = await _dataLayer.HealthEssentialsContext.LaboratoryServiceEntities
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);

        if (existingRecord == null)
        {
            return new()
            {
                Message = $"Laboratory service entity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingRecord.IsDeleted = true;
        existingRecord.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingRecord);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Laboratory  service entity with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };
    }
}