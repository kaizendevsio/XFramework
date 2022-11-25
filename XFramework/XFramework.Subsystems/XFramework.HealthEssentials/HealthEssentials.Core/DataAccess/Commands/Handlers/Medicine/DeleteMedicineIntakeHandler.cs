using HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Medicine;

public class DeleteMedicineIntakeHandler : CommandBaseHandler, IRequestHandler<DeleteMedicineIntakeCmd, CmdResponse<DeleteMedicineIntakeCmd>>
{
    public DeleteMedicineIntakeHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteMedicineIntakeCmd>> Handle(DeleteMedicineIntakeCmd request, CancellationToken cancellationToken)
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
        
        existingIntake.IsDeleted = true;
        existingIntake.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingIntake);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Medicine Intake with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };

    }
}