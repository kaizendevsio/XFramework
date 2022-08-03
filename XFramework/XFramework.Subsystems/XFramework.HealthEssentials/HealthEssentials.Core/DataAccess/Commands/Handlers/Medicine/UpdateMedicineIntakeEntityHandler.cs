using HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Medicine;

public class UpdateMedicineIntakeEntityHandler : CommandBaseHandler, IRequestHandler<UpdateMedicineIntakeEntityCmd, CmdResponse<UpdateMedicineIntakeEntityCmd>>
{
    public UpdateMedicineIntakeEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateMedicineIntakeEntityCmd>> Handle(UpdateMedicineIntakeEntityCmd request, CancellationToken cancellationToken)
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
        var updatedIntakeEntity = request.Adapt(existingIntakeEntity);

        _dataLayer.HealthEssentialsContext.Update(updatedIntakeEntity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Medicine Intake Entity with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };

    }
}