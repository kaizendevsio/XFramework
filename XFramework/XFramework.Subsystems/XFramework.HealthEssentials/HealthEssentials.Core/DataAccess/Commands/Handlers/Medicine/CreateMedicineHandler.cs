using HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Medicine;

public class CreateMedicineHandler : CommandBaseHandler, IRequestHandler<CreateMedicineCmd, CmdResponse<CreateMedicineCmd>>
{
    public CreateMedicineHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateMedicineCmd>> Handle(CreateMedicineCmd request, CancellationToken cancellationToken)
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

        var medicine = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.Medicine>();
        medicine.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        medicine.Entity = entity;
        
        await _dataLayer.HealthEssentialsContext.Medicines.AddAsync(medicine, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(medicine.Guid);
        return new()
        {
            Message = $"Medicine with Guid {medicine.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}