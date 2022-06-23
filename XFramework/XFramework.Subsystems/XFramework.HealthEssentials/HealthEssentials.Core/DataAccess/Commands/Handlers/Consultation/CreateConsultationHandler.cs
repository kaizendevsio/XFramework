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
        var entity = await _dataLayer.HealthEssentialsContext.ConsultationEntities
            .FirstOrDefaultAsync(i => i.Guid == $"{request.EntityGuid}", CancellationToken.None);
       
        if (entity is null)
        {
            return new ()
            {
                Message = $"Consultation entity with Guid {request.EntityGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var newRecord = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.Consultation>();
        newRecord.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        newRecord.Entity = entity;
        
        _dataLayer.HealthEssentialsContext.Add(newRecord);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Consultation with Guid {newRecord.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(newRecord.Guid)
            }
        };
    }
}