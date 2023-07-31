using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class CreateConsultationHandler : CommandBaseHandler, IRequestHandler<CreateConsultationCmd, CmdResponse<CreateConsultationCmd>>
{
    public CreateConsultationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateConsultationCmd>> Handle(CreateConsultationCmd request, CancellationToken cancellationToken)
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

        var consultation = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.Consultation>();
        consultation.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        consultation.Entity = consultationEntity;
        
        await _dataLayer.HealthEssentialsContext.Consultations.AddAsync(consultation, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(consultation.Guid);
        return new()
        {
            Message = $"Consultation with Guid {consultation.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}