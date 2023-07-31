using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class CreatePatientReminderHandler : CommandBaseHandler, IRequestHandler<CreatePatientReminderCmd, CmdResponse<CreatePatientReminderCmd>>
{
    public CreatePatientReminderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreatePatientReminderCmd>> Handle(CreatePatientReminderCmd request, CancellationToken cancellationToken)
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
        
        var consultationJobOrder = await _dataLayer.HealthEssentialsContext.ConsultationJobOrders.FirstOrDefaultAsync(x => x.Guid == $"{request.ConsultationJobOrderGuid}", CancellationToken.None);
        if (consultationJobOrder is null)
        {
            return new ()
            {
                Message = $"Consultation Job Order with Guid {request.ConsultationJobOrderGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var reminder = request.Adapt<PatientReminder>();
        reminder.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        reminder.Patient = patient;
        reminder.ConsultationJobOrder = consultationJobOrder;
        
        await _dataLayer.HealthEssentialsContext.PatientReminders.AddAsync(reminder, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(reminder.Guid);
        return new()
        {
            Message = $"Patient Reminder with Guid {reminder.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };


    }
}