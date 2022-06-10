using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class DeleteConsultationEntityHandler : CommandBaseHandler, IRequestHandler<DeleteConsultationEntityCmd, CmdResponse<DeleteConsultationEntityCmd>>
{
    public DeleteConsultationEntityHandler()
    {
        
    }
    public async Task<CmdResponse<DeleteConsultationEntityCmd>> Handle(DeleteConsultationEntityCmd request, CancellationToken cancellationToken)
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
        
        existingRecord.IsDeleted = true;
        existingRecord.IsEnabled = false;
        
        _dataLayer.HealthEssentialsContext.Update(existingRecord);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Consultation with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };
    }
}