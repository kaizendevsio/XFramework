using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class DeletePatientEntityHandler : CommandBaseHandler, IRequestHandler<DeletePatientEntityCmd, CmdResponse<DeletePatientEntityCmd>>
{
    public DeletePatientEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePatientEntityCmd>> Handle(DeletePatientEntityCmd request, CancellationToken cancellationToken)
    {
        var existingEntity = await _dataLayer.HealthEssentialsContext.PatientEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingEntity is null)
        {
            return new ()
            {
                Message = $"Patient Entity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingEntity.IsDeleted = true;
        existingEntity.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingEntity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Patient Entity with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}