using HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;
using HealthEssentials.Domain.Generics.Contracts;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Medicine;

public class CreateMedicineIntakeHandler : CommandBaseHandler, IRequestHandler<CreateMedicineIntakeCmd, CmdResponse<CreateMedicineIntakeCmd>>
{
    public CreateMedicineIntakeHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateMedicineIntakeCmd>> Handle(CreateMedicineIntakeCmd request, CancellationToken cancellationToken)
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
        
        var unit = await _dataLayer.HealthEssentialsContext.Units.FirstOrDefaultAsync(x => x.Guid == $"{request.UnitGuid}", CancellationToken.None);
        if (unit is null)
        {
            return new()
            {
                Message = $"Unit with Guid {request.UnitGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var intake = request.Adapt<MedicineIntake>();
        intake.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        intake.Unit = unit;
        intake.Type = entity;
        
        await _dataLayer.HealthEssentialsContext.MedicineIntakes.AddAsync(intake, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(intake.Guid);
        return new()
        {
            Message = $"Medicine Intake with Guid {intake.Guid} created",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
        
    }
}