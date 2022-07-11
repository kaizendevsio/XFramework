using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class UpdateConsultationJobOrderHandler : CommandBaseHandler, IRequestHandler<UpdateConsultationJobOrderCmd, CmdResponse<UpdateConsultationJobOrderCmd>>
{
    public UpdateConsultationJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateConsultationJobOrderCmd>> Handle(UpdateConsultationJobOrderCmd request, CancellationToken cancellationToken)
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
        
        var consultation = await _dataLayer.HealthEssentialsContext.Consultations
            .FirstOrDefaultAsync(x => x.Guid == $"{request.ConsultationGuid}", CancellationToken.None);
        
        if (consultation is null)
        {
            return new ()
            {
                Message = $"Consultation with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var schedule = await _dataLayer.HealthEssentialsContext.Schedules
            .FirstOrDefaultAsync(x => x.Guid == $"{request.ScheduleGuid}", CancellationToken.None);
        
        if (schedule is null)
        {
            return new ()
            {
                Message = $"Schedule with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var updatedJobOrder = request.Adapt(existingJobOrder);
        updatedJobOrder.Consultation = consultation;
        updatedJobOrder.Schedule = schedule;

        _dataLayer.HealthEssentialsContext.Update(updatedJobOrder);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Consultation Job Order with Guid {updatedJobOrder.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
            Request = new()
            {
                Guid = Guid.Parse(updatedJobOrder.Guid)
            }
        };
    }
}