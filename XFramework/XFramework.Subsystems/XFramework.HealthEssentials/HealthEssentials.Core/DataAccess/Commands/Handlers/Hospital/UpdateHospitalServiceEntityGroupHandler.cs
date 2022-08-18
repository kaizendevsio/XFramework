using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class UpdateHospitalServiceEntityGroupHandler : CommandBaseHandler, IRequestHandler<UpdateHospitalServiceEntityGroupCmd, CmdResponse<UpdateHospitalServiceEntityGroupCmd>>
{
    public UpdateHospitalServiceEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateHospitalServiceEntityGroupCmd>> Handle(UpdateHospitalServiceEntityGroupCmd request, CancellationToken cancellationToken)
    {
        var existingGroup = await _dataLayer.HealthEssentialsContext.HospitalServiceEntityGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingGroup is null)
        {
            return new ()
            {
                Message = $"Hospital Service Entity Group with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedGroup = request.Adapt(existingGroup);

        _dataLayer.HealthEssentialsContext.Update(updatedGroup);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Hospital Service Entity Group with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}