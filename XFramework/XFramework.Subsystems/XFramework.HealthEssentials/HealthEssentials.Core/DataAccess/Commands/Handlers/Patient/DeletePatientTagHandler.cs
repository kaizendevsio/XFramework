using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class DeletePatientTagHandler : CommandBaseHandler, IRequestHandler<DeletePatientTagCmd, CmdResponse<DeletePatientTagCmd>>
{
    public DeletePatientTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeletePatientTagCmd>> Handle(DeletePatientTagCmd request, CancellationToken cancellationToken)
    {
        var existingTag = await _dataLayer.HealthEssentialsContext.PatientTags.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingTag is null)
        {
            return new ()
            {
                Message = $"Patient tag with guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingTag.IsDeleted = true;
        existingTag.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingTag);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Patient tag with guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}