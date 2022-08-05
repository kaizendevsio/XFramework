using HealthEssentials.Core.DataAccess.Commands.Entity.Laboratory;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Laboratory;

public class CreateLaboratoryJobOrderHandler : CommandBaseHandler, IRequestHandler<CreateLaboratoryJobOrderCmd, CmdResponse<CreateLaboratoryJobOrderCmd>>
{
    public CreateLaboratoryJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateLaboratoryJobOrderCmd>> Handle(CreateLaboratoryJobOrderCmd request, CancellationToken cancellationToken)
    {
        var consultationJobOrder = await _dataLayer.HealthEssentialsContext.ConsultationJobOrders.FirstOrDefaultAsync(x => x.Guid == $"{request.ConsultationJobOrderGuid}", CancellationToken.None);
        if (consultationJobOrder is null)
        {
            return new ()
            {
                Message = $"Job Order with Guid {request.ConsultationJobOrderGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var patient = await _dataLayer.HealthEssentialsContext.Patients.FirstOrDefaultAsync(x => x.Guid == $"{request.PatientGuid}", CancellationToken.None);
        if (patient is null)
        {
            return new ()
            {
                Message = $"Patient with Guid {request.PatientGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var location = await _dataLayer.HealthEssentialsContext.LaboratoryLocations.FirstOrDefaultAsync(x => x.Guid == $"{request.LaboratoryLocationGuid}", CancellationToken.None);
        if (location is null)
        {
            return new ()
            {
                Message = $"Laboratory Location with Guid {request.LaboratoryLocationGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        var schedule = await _dataLayer.HealthEssentialsContext.Schedules.FirstOrDefaultAsync(x => x.Guid == $"{request.ScheduleGuid}", CancellationToken.None);
        if (schedule is null)
        {
            return new ()
            {
                Message = $"Schedule with Guid {request.ScheduleGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var jobOrder = request.Adapt<LaboratoryJobOrder>();
        jobOrder.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        jobOrder.ConsultationJobOrder = consultationJobOrder;
        jobOrder.Patient = patient;
        jobOrder.LaboratoryLocation = location;
        jobOrder.Schedule = schedule;
        
        await _dataLayer.HealthEssentialsContext.LaboratoryJobOrders.AddAsync(jobOrder, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(jobOrder.Guid);
        return new()
        {
            Message = $"Laboratory Job Order with Guid {jobOrder.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}