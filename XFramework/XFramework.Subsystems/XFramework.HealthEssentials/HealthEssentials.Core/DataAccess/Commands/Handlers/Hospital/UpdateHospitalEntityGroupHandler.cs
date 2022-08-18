using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class UpdateHospitalEntityGroupHandler : CommandBaseHandler, IRequestHandler<UpdateHospitalEntityGroupCmd, CmdResponse<UpdateHospitalEntityGroupCmd>>
{
    public UpdateHospitalEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateHospitalEntityGroupCmd>> Handle(UpdateHospitalEntityGroupCmd request, CancellationToken cancellationToken)
    {
        var existingGroup = await _dataLayer.HealthEssentialsContext.HospitalEntityGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingGroup is null)
        {
            return new ()
            {
                Message = $"Hospital Entity Group with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedGroup = request.Adapt(existingGroup);

        _dataLayer.HealthEssentialsContext.Update(updatedGroup);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(cancellationToken);
        
        return new CmdResponse<UpdateHospitalEntityGroupCmd>
        {
            Message = $"Hospital Entity Group with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}