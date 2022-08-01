using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class UpdatePatientReminderHandler : CommandBaseHandler, IRequestHandler<UpdatePatientReminderCmd, CmdResponse<UpdatePatientReminderCmd>>
{
    public UpdatePatientReminderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdatePatientReminderCmd>> Handle(UpdatePatientReminderCmd request, CancellationToken cancellationToken)
    {
        var existingReminder = await _dataLayer.HealthEssentialsContext.PatientReminders.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingReminder is null)
        {
            return new ()
            {
                Message = $"Patient Reminder with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedReminder = request.Adapt(existingReminder);

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
            updatedReminder.Patient = patient;
        }

        if (request.ConsultationJobOrderGuid is null)
        {
            var consultationJobOrder = await _dataLayer.HealthEssentialsContext.ConsultationJobOrders.FirstOrDefaultAsync(x => x.Guid == $"{request.ConsultationJobOrderGuid}", CancellationToken.None);
            if (consultationJobOrder is null)
            {
                return new ()
                {
                    Message = $"Consultation Job Order with Guid {request.ConsultationJobOrderGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            } 
            updatedReminder.ConsultationJobOrder = consultationJobOrder;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedReminder);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Patient Reminder with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}