using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class DeleteConsultationJobOrderHandler : CommandBaseHandler, IRequestHandler<DeleteConsultationJobOrderCmd, CmdResponse<DeleteConsultationJobOrderCmd>>
{
    public DeleteConsultationJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteConsultationJobOrderCmd>> Handle(DeleteConsultationJobOrderCmd request, CancellationToken cancellationToken)
    {
        var existingJobOrder = await _dataLayer.HealthEssentialsContext.ConsultationJobOrders
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (existingJobOrder == null)
        {
            return new()
            {
                Message = $"Consultation Job Order with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingJobOrder.IsDeleted = true;
        existingJobOrder.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingJobOrder);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Consultation Job Order with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}