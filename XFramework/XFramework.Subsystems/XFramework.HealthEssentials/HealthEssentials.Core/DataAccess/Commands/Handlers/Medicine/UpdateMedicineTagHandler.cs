using HealthEssentials.Core.DataAccess.Commands.Entity.Medicine;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Medicine;

public class UpdateMedicineTagHandler : CommandBaseHandler, IRequestHandler<UpdateMedicineTagCmd, CmdResponse<UpdateMedicineTagCmd>>
{
    public UpdateMedicineTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateMedicineTagCmd>> Handle(UpdateMedicineTagCmd request, CancellationToken cancellationToken)
    {
        var existingTag = await _dataLayer.HealthEssentialsContext.MedicineTags.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingTag is null)
        {
            return new ()
            {
                Message = $"Medicine Tag with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedTag = request.Adapt(existingTag);

        if (request.MedicineGuid is null)
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
            updatedTag.Medicine = medicine;
        }

        if (request.TagGuid is null)
        {
            var tag = await _dataLayer.HealthEssentialsContext.Tags.FirstOrDefaultAsync(x => x.Guid == $"{request.TagGuid}", CancellationToken.None);
            if (tag is null)
            {
                return new()
                {
                    Message = $"Tag with Guid {request.TagGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedTag.Tag = tag;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedTag);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Medicine Tag with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };

    }
}