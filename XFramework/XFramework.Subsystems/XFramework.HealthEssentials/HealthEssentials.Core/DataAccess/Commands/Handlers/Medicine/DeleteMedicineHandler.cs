using HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Medicine;

public class DeleteMedicineHandler : CommandBaseHandler, IRequestHandler<DeleteMedicineCmd, CmdResponse<DeleteMedicineCmd>>
{
    public DeleteMedicineHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteMedicineCmd>> Handle(DeleteMedicineCmd request, CancellationToken cancellationToken)
    {
        var existingMedicine = await _dataLayer.HealthEssentialsContext.Medicines.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingMedicine is null)
        {
            return new ()
            {
                Message = $"Medicine with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingMedicine.IsDeleted = true;
        existingMedicine.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingMedicine);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Medicine with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}