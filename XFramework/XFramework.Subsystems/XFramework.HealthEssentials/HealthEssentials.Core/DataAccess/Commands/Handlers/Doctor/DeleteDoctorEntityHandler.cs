using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Doctor;

public class DeleteDoctorEntityHandler : CommandBaseHandler, IRequestHandler<DeleteDoctorEntityCmd, CmdResponse<DeleteDoctorEntityCmd>>
{
    public DeleteDoctorEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteDoctorEntityCmd>> Handle(DeleteDoctorEntityCmd request, CancellationToken cancellationToken)
    {
        var existingEntity = await _dataLayer.HealthEssentialsContext.DoctorEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingEntity is null)
        {
            return new ()
            {
                Message = $"Doctor Entity with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingEntity.IsDeleted = true;
        existingEntity.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingEntity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Doctor Entity with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}