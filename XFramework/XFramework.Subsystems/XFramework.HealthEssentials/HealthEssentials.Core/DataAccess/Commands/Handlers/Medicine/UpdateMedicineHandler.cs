using HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Medicine;

public class UpdateMedicineHandler : CommandBaseHandler, IRequestHandler<UpdateMedicineCmd, CmdResponse<UpdateMedicineCmd>>
{
    public UpdateMedicineHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateMedicineCmd>> Handle(UpdateMedicineCmd request, CancellationToken cancellationToken)
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
        var updatedMedicine = request.Adapt(existingMedicine);

        if (request.EntityGuid is null)
        {
            var entity = await _dataLayer.HealthEssentialsContext.MedicineEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.EntityGuid}", CancellationToken.None);
            if (entity is null)
            {
                return new()
                {
                    Message = $"Medicine with Guid {request.EntityGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedMedicine.Type = entity;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedMedicine);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Medicine with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}