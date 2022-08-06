using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class DeleteConsultationJobOrderLaboratoryHandler : CommandBaseHandler, IRequestHandler<DeleteConsultationJobOrderLaboratoryCmd, CmdResponse<DeleteConsultationJobOrderLaboratoryCmd>>
{
    public DeleteConsultationJobOrderLaboratoryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteConsultationJobOrderLaboratoryCmd>> Handle(DeleteConsultationJobOrderLaboratoryCmd request, CancellationToken cancellationToken)
    {
        var existingJobOrderLaboratory = await _dataLayer.HealthEssentialsContext.ConsultationJobOrderLaboratories.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingJobOrderLaboratory is null)
        {
            return new ()
            {
                Message = $"Consultation Job Order Laboratory with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingJobOrderLaboratory.IsDeleted = true;
        existingJobOrderLaboratory.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingJobOrderLaboratory);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Consultation Job Order Laboratory with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}