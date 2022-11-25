using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class UpdatePharmacyJobOrderHandler : CommandBaseHandler, IRequestHandler<UpdatePharmacyJobOrderCmd, CmdResponse<UpdatePharmacyJobOrderCmd>>
{
    public UpdatePharmacyJobOrderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdatePharmacyJobOrderCmd>> Handle(UpdatePharmacyJobOrderCmd request, CancellationToken cancellationToken)
    {
        var existingJobOrder = await _dataLayer.HealthEssentialsContext.PharmacyJobOrders.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingJobOrder is null)
        {
            return new ()
            {
                Message = $"Pharmacy Job Order with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedJobOrder = request.Adapt(existingJobOrder);

        if (request.PharmacyLocationGuid is null)
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
            updatedJobOrder.PharmacyLocation = location;
        }

        if (request.ScheduleGuid is null)
        {
            var schedule = await _dataLayer.HealthEssentialsContext.Schedules.FirstOrDefaultAsync(x => x.Guid == $"{request.ScheduleGuid}", CancellationToken.None);
            if (schedule is null)
            {
                return new ()
                {
                    Message = $"Schedule with Guid {request.ScheduleGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedJobOrder.Schedule = schedule;
        }

        if (request.PatientGuid is null)
        {
            var patient = await _dataLayer.HealthEssentialsContext.Patients.FirstOrDefaultAsync(x => x.Guid == $"{request.PatientGuid}", CancellationToken.None);
            if (patient is null)
            {
                return new ()
                {
                    Message = $"Patient with Guid {request.PatientGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedJobOrder.Patient = patient;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedJobOrder);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Pharmacy Job Order with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.Accepted
        };

    }
}