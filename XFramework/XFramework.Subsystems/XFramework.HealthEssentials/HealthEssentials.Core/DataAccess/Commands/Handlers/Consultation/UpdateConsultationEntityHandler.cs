using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class UpdateConsultationEntityHandler : CommandBaseHandler, IRequestHandler<UpdateConsultationEntityCmd, CmdResponse<UpdateConsultationEntityCmd>>
{
    public UpdateConsultationEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateConsultationEntityCmd>> Handle(UpdateConsultationEntityCmd request, CancellationToken cancellationToken)
    {
        var existingConsultationEntity = await _dataLayer.HealthEssentialsContext.ConsultationEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingConsultationEntity == null)
        {
            return new()
            {
                Message = $"Consultation entity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedConsultationEntity = request.Adapt(existingConsultationEntity);

        if (request.GroupGuid is not null)
        {
            var entityGroup = await _dataLayer.HealthEssentialsContext.ConsultationEntityGroups.FirstOrDefaultAsync(i => i.Guid == $"{request.GroupGuid}",CancellationToken.None);
            if (entityGroup is null)
            {
                return new ()
                {
                    Message = $"Consultation entity group with Guid {request.GroupGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedConsultationEntity.Group = entityGroup;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedConsultationEntity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Consultation entity with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}