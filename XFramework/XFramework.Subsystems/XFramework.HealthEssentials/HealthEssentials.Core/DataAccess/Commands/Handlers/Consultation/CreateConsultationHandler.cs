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
        var type = await _dataLayer.HealthEssentialsContext.ConsultationEntities
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.TypeGuid}", cancellationToken: cancellationToken);
       
        if (type is null)
        {
            return new ()
            {
                Message = $"Consultation type with Guid {request.TypeGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var entity = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.Consultation>();
        entity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        entity.EntityId = type.Id;
        
        _dataLayer.HealthEssentialsContext.Consultations.Add(entity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Consultation with Guid {entity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(entity.Guid)
            }
        };
    }
}