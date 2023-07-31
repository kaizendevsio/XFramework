using HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Medicine;

public class CreateMedicineIntakeEntityHandler : CommandBaseHandler, IRequestHandler<CreateMedicineIntakeEntityCmd, CmdResponse<CreateMedicineIntakeEntityCmd>>
{
    public CreateMedicineIntakeEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateMedicineIntakeEntityCmd>> Handle(CreateMedicineIntakeEntityCmd request, CancellationToken cancellationToken)
    {
        var intakeEntity = request.Adapt<MedicineIntakeEntity>();
        intakeEntity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        
        await _dataLayer.HealthEssentialsContext.MedicineIntakeEntities.AddAsync(intakeEntity,CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(intakeEntity.Guid);
        return new()
        {
            Message = $"Medicine Intake Entity with Guid {intakeEntity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}