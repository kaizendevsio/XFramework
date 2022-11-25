using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class DeletePatientHandler : CommandBaseHandler, IRequestHandler<DeletePatientCmd, CmdResponse<DeletePatientCmd>>
{
    public DeletePatientHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePatientCmd>> Handle(DeletePatientCmd request, CancellationToken cancellationToken)
    {
        var existingPatient = await _dataLayer.HealthEssentialsContext.Patients.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingPatient == null)
        {
            return new()
            {
                Message = $"Patient with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingPatient.IsDeleted = true;
        existingPatient.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingPatient);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new()
        {
            Message = $"Patient with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}