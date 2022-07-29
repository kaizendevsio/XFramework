using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class CreatePharmacyJobOrderHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyJobOrderCmd, CmdResponse<CreatePharmacyJobOrderCmd>>
{
    public CreatePharmacyJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreatePharmacyJobOrderCmd>> Handle(CreatePharmacyJobOrderCmd request, CancellationToken cancellationToken)
    {
        var location = await _dataLayer.HealthEssentialsContext.PharmacyLocations.FirstOrDefaultAsync(x => x.Guid == $"{request.PharmacyLocationGuid}", CancellationToken.None);
        if (location is null)
        {
            return new ()
            {
                Message = $"Pharmacy Location with Guid {request.PharmacyLocationGuid} does not exist",
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
        
        var patient = await _dataLayer.HealthEssentialsContext.Patients.FirstOrDefaultAsync(x => x.Guid == $"{request.PatientGuid}", CancellationToken.None);
        if (patient is null)
        {
            return new ()
            {
                Message = $"Patient with Guid {request.PatientGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var jobOrder = request.Adapt<PharmacyJobOrder>();
        jobOrder.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        jobOrder.PharmacyLocation = location;
        jobOrder.Schedule = schedule;
        jobOrder.Patient = patient;
        
        await _dataLayer.HealthEssentialsContext.PharmacyJobOrders.AddAsync(jobOrder, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(jobOrder.Guid);
        return new()
        {
            Message = $"Pharmacy Job Order with Guid {jobOrder.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
        
    }
}