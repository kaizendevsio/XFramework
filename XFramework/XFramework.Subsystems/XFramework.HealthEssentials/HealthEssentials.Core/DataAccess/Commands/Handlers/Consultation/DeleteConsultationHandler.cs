using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class DeleteConsultationHandler : CommandBaseHandler, IRequestHandler<DeleteConsultationCmd, CmdResponse<DeleteConsultationCmd>>
{
    public DeleteConsultationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteConsultationCmd>> Handle(DeleteConsultationCmd request, CancellationToken cancellationToken)
    {
        var existingConsultation = await _dataLayer.HealthEssentialsContext.Consultations
            .FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingConsultation is null)
        {
            return new ()
            {
                Message = $"Consultation with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            }; 
        }
        
        existingConsultation.IsDeleted = true;
        existingConsultation.IsEnabled = false;
        
        _dataLayer.HealthEssentialsContext.Update(existingConsultation);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Consultation with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true
        };
    }
}