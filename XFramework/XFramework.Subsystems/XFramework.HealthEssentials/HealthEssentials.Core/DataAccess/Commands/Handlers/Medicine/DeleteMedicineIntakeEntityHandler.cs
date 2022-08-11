using HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Medicine;

public class DeleteMedicineIntakeEntityHandler : CommandBaseHandler, IRequestHandler<DeleteMedicineIntakeEntityCmd, CmdResponse<DeleteMedicineIntakeEntityCmd>>
{
    public DeleteMedicineIntakeEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteMedicineIntakeEntityCmd>> Handle(DeleteMedicineIntakeEntityCmd request, CancellationToken cancellationToken)
    {
        var existingIntakeEntity = await _dataLayer.HealthEssentialsContext.MedicineIntakeEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingIntakeEntity is null)
        {
            return new ()
            {
                Message = $"Medicine Intake Entity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingIntakeEntity.IsDeleted = true;
        existingIntakeEntity.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingIntakeEntity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Medicine Intake Entity with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
        
    }
}