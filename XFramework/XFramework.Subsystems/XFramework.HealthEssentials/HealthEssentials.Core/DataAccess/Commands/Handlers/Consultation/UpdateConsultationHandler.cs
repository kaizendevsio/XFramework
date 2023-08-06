using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class UpdateConsultationHandler : CommandBaseHandler, IRequestHandler<UpdateConsultationCmd, CmdResponse<UpdateConsultationCmd>>
{
    public UpdateConsultationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateConsultationCmd>> Handle(UpdateConsultationCmd request, CancellationToken cancellationToken)
    {
        var existingConsultation = await _dataLayer.HealthEssentialsContext.Consultations.FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingConsultation is null)
        {
            return new ()
            {
                Message = $"Consultation with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            }; 
        }
        var updatedConsultation = request.Adapt(existingConsultation);

        if (request.EntityGuid is not null)
        {
            var consultationEntity = await _dataLayer.HealthEssentialsContext.ConsultationEntities.FirstOrDefaultAsync(i => i.Guid == $"{request.EntityGuid}", CancellationToken.None);
            if (consultationEntity is null)
            {
                return new ()
                {
                    Message = $"Consultation entity with Guid {request.EntityGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedConsultation.Type = consultationEntity;
        }
        
        _dataLayer.HealthEssentialsContext.Update(updatedConsultation);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Consultation with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}