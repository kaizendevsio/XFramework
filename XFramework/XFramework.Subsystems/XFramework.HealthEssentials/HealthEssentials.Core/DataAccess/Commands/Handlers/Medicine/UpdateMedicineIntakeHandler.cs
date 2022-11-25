using HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Medicine;

public class UpdateMedicineIntakeHandler : CommandBaseHandler, IRequestHandler<UpdateMedicineIntakeCmd, CmdResponse<UpdateMedicineIntakeCmd>>
{
    public UpdateMedicineIntakeHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateMedicineIntakeCmd>> Handle(UpdateMedicineIntakeCmd request, CancellationToken cancellationToken)
    {
        var existingIntake = await _dataLayer.HealthEssentialsContext.MedicineIntakes.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingIntake is null)
        {
            return new ()
            {
                Message = $"Medicine Intake with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedIntake = request.Adapt(existingIntake);

        if (request.EntityGuid is null)
        {
            var entity = await _dataLayer.HealthEssentialsContext.MedicineIntakeEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.EntityGuid}", CancellationToken.None);
            if (entity is null)
            {
                return new()
                {
                    Message = $"Medicine Intake Entity with Guid {request.EntityGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedIntake.Entity = entity;
        }

        if (request.UnitGuid is null)
        {
            var unit = await _dataLayer.HealthEssentialsContext.Units.FirstOrDefaultAsync(x => x.Guid == $"{request.UnitGuid}", CancellationToken.None);
            if (unit is null)
            {
                return new()
                {
                    Message = $"Unit with Guid {request.UnitGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedIntake.Unit = unit;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedIntake);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Medicine Intake with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}