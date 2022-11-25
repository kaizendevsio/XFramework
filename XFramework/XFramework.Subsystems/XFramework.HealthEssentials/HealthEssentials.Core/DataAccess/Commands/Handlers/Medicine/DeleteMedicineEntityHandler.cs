using HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Medicine;

public class DeleteMedicineEntityHandler : CommandBaseHandler, IRequestHandler<DeleteMedicineEntityCmd, CmdResponse<DeleteMedicineEntityCmd>>
{
    public DeleteMedicineEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteMedicineEntityCmd>> Handle(DeleteMedicineEntityCmd request, CancellationToken cancellationToken)
    {
        var existingEntity = await _dataLayer.HealthEssentialsContext.MedicineEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingEntity is null)
        {
            return new ()
            {
                Message = $"Medicine Entity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingEntity.IsDeleted = true;
        existingEntity.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingEntity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Medicine Entity with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}