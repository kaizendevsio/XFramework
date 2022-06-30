using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class CreateConsultationEntityHandler : CommandBaseHandler, IRequestHandler<CreateConsultationEntityCmd, CmdResponse<CreateConsultationEntityCmd>>
{
    public CreateConsultationEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateConsultationEntityCmd>> Handle(CreateConsultationEntityCmd request, CancellationToken cancellationToken)
    {
        var entityGroup = await _dataLayer.HealthEssentialsContext.ConsultationEntityGroups
            .FirstOrDefaultAsync(i => i.Guid == $"{request.GroupGuid}", CancellationToken.None);
       
        if (entityGroup is null)
        {
            return new ()
            {
                Message = $"Consultation entity group with Guid {request.GroupGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var consultationEntity = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.ConsultationEntity>();
        consultationEntity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        consultationEntity.Group = entityGroup;
        
        await _dataLayer.HealthEssentialsContext.ConsultationEntities.AddAsync(consultationEntity, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Consultation entity with Guid {consultationEntity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(consultationEntity.Guid)
            }
        };
    }
}