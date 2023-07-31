using HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Medicine;

public class CreateMedicineTagHandler : CommandBaseHandler, IRequestHandler<CreateMedicineTagCmd, CmdResponse<CreateMedicineTagCmd>>
{
    public CreateMedicineTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateMedicineTagCmd>> Handle(CreateMedicineTagCmd request, CancellationToken cancellationToken)
    {
        var medicine = await _dataLayer.HealthEssentialsContext.Medicines.FirstOrDefaultAsync(x => x.Guid == $"{request.MedicineGuid}", CancellationToken.None);
        if (medicine is null)
        {
            return new()
            {
                Message = $"Medicine with Guid {request.MedicineGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var tag = await _dataLayer.HealthEssentialsContext.Tags.FirstOrDefaultAsync(x => x.Guid == $"{request.TagGuid}", CancellationToken.None);
        if (tag is null)
        {
            return new()
            {
                Message = $"Tag with Guid {request.TagGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var medicineTag = request.Adapt<MedicineTag>();
        medicineTag.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        medicineTag.Medicine = medicine;
        medicineTag.Tag = tag;
        
        await _dataLayer.HealthEssentialsContext.MedicineTags.AddAsync(medicineTag, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(medicineTag.Guid);
        return new()
        {
            Message = $"Medicine Tag with Guid {medicineTag.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
        
    }
}