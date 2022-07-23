using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class DeletePatientConsultationHandler : CommandBaseHandler, IRequestHandler<DeletePatientConsultationCmd, CmdResponse<DeletePatientConsultationCmd>>
{
    public DeletePatientConsultationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeletePatientConsultationCmd>> Handle(DeletePatientConsultationCmd request, CancellationToken cancellationToken)
    {
        var existingPatientConsultation = await _dataLayer.HealthEssentialsContext.PatientConsultations.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingPatientConsultation is null)
        {
            return new ()
            {
                Message = $"Patient Consultation with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingPatientConsultation.IsDeleted = true;
        existingPatientConsultation.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingPatientConsultation);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Patient Consultation with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}