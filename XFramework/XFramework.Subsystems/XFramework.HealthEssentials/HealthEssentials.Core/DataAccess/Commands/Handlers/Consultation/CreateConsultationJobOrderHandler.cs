using HealthEssentials.Core.DataAccess.Commands.Entity.Consultation;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Consultation;

public class CreateConsultationJobOrderHandler : CommandBaseHandler, IRequestHandler<CreateConsultationJobOrderCmd, CmdResponse<CreateConsultationJobOrderCmd>>
{
    public CreateConsultationJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateConsultationJobOrderCmd>> Handle(CreateConsultationJobOrderCmd request, CancellationToken cancellationToken)
    {
        var consultation = await _dataLayer.HealthEssentialsContext.Consultations
            .FirstOrDefaultAsync(x => x.Guid == $"{request.ConsultationGuid}", CancellationToken.None);
        
        if (consultation is null)
        {
            return new ()
            {
                Message = $"Consultation with Guid {request.ConsultationGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var schedule = await _dataLayer.HealthEssentialsContext.Schedules
            .FirstOrDefaultAsync(x => x.Guid == $"{request.ScheduleGuid}", CancellationToken.None);
        
        if (schedule is null)
        {
            return new ()
            {
                Message = $"Schedule with Guid {request.ScheduleGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var jobOrder = request.Adapt<ConsultationJobOrder>();
        jobOrder.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        jobOrder.Consultation = consultation;
        jobOrder.Schedule = schedule;
        
        await _dataLayer.HealthEssentialsContext.ConsultationJobOrders.AddAsync(jobOrder, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(jobOrder.Guid);
        return new()
        {
            Message = $"Consultation job order with Guid {jobOrder.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}