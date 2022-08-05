using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Doctor;

public class UpdateDoctorEntityHandler : CommandBaseHandler, IRequestHandler<UpdateDoctorEntityCmd, CmdResponse<UpdateDoctorEntityCmd>>
{
    public UpdateDoctorEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateDoctorEntityCmd>> Handle(UpdateDoctorEntityCmd request, CancellationToken cancellationToken)
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
        var updatedEntity = request.Adapt(existingEntity);

        if (request.GroupGuid is null)
        {
            var group = await _dataLayer.HealthEssentialsContext.DoctorEntityGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.GroupGuid}", CancellationToken.None);
            if (group is null)
            {
                return new ()
                {
                    Message = $"Group with guid {request.GroupGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedEntity.Group = group;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedEntity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Doctor Entity with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}