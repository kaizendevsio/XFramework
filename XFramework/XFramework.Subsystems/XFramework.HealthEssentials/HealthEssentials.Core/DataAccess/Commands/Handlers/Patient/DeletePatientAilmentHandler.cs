using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class DeletePatientAilmentHandler : CommandBaseHandler, IRequestHandler<DeletePatientAilmentCmd, CmdResponse<DeletePatientAilmentCmd>>
{
    public DeletePatientAilmentHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePatientAilmentCmd>> Handle(DeletePatientAilmentCmd request, CancellationToken cancellationToken)
    {
        var existingPatientAilment = await _dataLayer.HealthEssentialsContext.PatientAilments.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingPatientAilment is null)
        {
            return new ()
            {
                Message = $"PatientAilment with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingPatientAilment.IsDeleted = true;
        existingPatientAilment.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingPatientAilment); 
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"PatientAilment with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}