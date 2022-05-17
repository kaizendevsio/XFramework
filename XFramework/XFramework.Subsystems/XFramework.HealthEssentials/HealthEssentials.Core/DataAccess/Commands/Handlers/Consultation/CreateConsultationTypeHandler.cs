using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class CreateConsultationTypeHandler : CommandBaseHandler, IRequestHandler<CreateConsultationTypeCmd, CmdResponse<CreateConsultationTypeCmd>>
{
    public CreateConsultationTypeHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateConsultationTypeCmd>> Handle(CreateConsultationTypeCmd request, CancellationToken cancellationToken)
    {
        var type = await _dataLayer.HealthEssentialsContext.ConsultationEntityGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.GroupGuid}", cancellationToken: cancellationToken);
       
        if (type is null)
        {
            return new ()
            {
                Message = $"Consultation type group with Guid {request.GroupGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var entity = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.ConsultationEntity>();
        entity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        entity.GroupId = type.Id;
        
        _dataLayer.HealthEssentialsContext.ConsultationEntities.Add(entity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Consultation type with Guid {entity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(entity.Guid)
            }
        };
    }
}