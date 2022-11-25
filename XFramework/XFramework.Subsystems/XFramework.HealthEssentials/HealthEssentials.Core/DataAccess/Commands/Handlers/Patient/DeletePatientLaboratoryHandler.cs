using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class DeletePatientLaboratoryHandler : CommandBaseHandler, IRequestHandler<DeletePatientLaboratoryCmd, CmdResponse<DeletePatientLaboratoryCmd>>
{
    public DeletePatientLaboratoryHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePatientLaboratoryCmd>> Handle(DeletePatientLaboratoryCmd request, CancellationToken cancellationToken)
    {
        var existingPatientLaboratory = await _dataLayer.HealthEssentialsContext.PatientLaboratories.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingPatientLaboratory is null)
        {
            return new ()
            {
                Message = $"Patient Laboratory with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingPatientLaboratory.IsDeleted = true;
        existingPatientLaboratory.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingPatientLaboratory);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Patient Laboratory with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
        
    }
}