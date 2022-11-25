using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class UpdateHospitalEntityHandler : CommandBaseHandler, IRequestHandler<UpdateHospitalEntityCmd, CmdResponse<UpdateHospitalEntityCmd>>
{
    public UpdateHospitalEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateHospitalEntityCmd>> Handle(UpdateHospitalEntityCmd request, CancellationToken cancellationToken)
    {
        var existingEntity = await _dataLayer.HealthEssentialsContext.HospitalEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingEntity is null)
        {
            return new ()
            {
                Message = $"Hospital Entity with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedEntity = request.Adapt(existingEntity);

        if (request.GroupGuid is null)
        {
            var group = await _dataLayer.HealthEssentialsContext.HospitalEntityGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.GroupGuid}", CancellationToken.None);
            if (group is null)
            {
                return new ()
                {
                    Message = $"Hospital Entity Group with Guid {request.GroupGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedEntity.Group = group;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedEntity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Hospital Entity with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}