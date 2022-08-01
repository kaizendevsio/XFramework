using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class DeletePatientReminderHandler : CommandBaseHandler, IRequestHandler<DeletePatientReminderCmd, CmdResponse<DeletePatientReminderCmd>>
{
    public DeletePatientReminderHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePatientReminderCmd>> Handle(DeletePatientReminderCmd request, CancellationToken cancellationToken)
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

        existingReminder.IsDeleted = true;
        existingReminder.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingReminder);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Patient Reminder with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };

    }
}