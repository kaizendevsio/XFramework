using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class DeleteConsultationEntityHandler : CommandBaseHandler, IRequestHandler<DeleteConsultationEntityCmd, CmdResponse<DeleteConsultationEntityCmd>>
{
    public DeleteConsultationEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteConsultationEntityCmd>> Handle(DeleteConsultationEntityCmd request, CancellationToken cancellationToken)
    {
        var existingConsultationEntity = await _dataLayer.HealthEssentialsContext.ConsultationEntities
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);

        if (existingConsultationEntity == null)
        {
            return new()
            {
                Message = $"Consultation with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingConsultationEntity.IsDeleted = true;
        existingConsultationEntity.IsEnabled = false;
        
        _dataLayer.HealthEssentialsContext.Update(existingConsultationEntity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Consultation with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };
    }
}