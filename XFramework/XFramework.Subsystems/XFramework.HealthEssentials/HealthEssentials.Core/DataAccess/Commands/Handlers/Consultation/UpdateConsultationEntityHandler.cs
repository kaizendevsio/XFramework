using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;
using HealthEssentials.Domain.DataTransferObjects;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class UpdateConsultationEntityHandler : CommandBaseHandler, IRequestHandler<UpdateConsultationEntityCmd, CmdResponse<UpdateConsultationEntityCmd>>
{
    public UpdateConsultationEntityHandler()
    {
        
    }
    public async Task<CmdResponse<UpdateConsultationEntityCmd>> Handle(UpdateConsultationEntityCmd request, CancellationToken cancellationToken)
    {
        var existingRecord = await _dataLayer.HealthEssentialsContext.ConsultationEntities
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);

        if (existingRecord == null)
        {
            return new()
            {
                Message = $"Consultation with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var entityGroup = await _dataLayer.HealthEssentialsContext.ConsultationEntityGroups
            .FirstOrDefaultAsync(i => i.Guid == $"{request.GroupGuid}", cancellationToken: cancellationToken);
       
        if (entityGroup is null)
        {
            return new ()
            {
                Message = $"Consultation type group with Guid {request.GroupGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var updatedRecord = request.Adapt(existingRecord);
        updatedRecord.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        updatedRecord.Group = entityGroup;
        
        _dataLayer.HealthEssentialsContext.ConsultationEntities.Update(updatedRecord);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Consultation entity with Guid {updatedRecord.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(updatedRecord.Guid)
            }
        };
    }
}