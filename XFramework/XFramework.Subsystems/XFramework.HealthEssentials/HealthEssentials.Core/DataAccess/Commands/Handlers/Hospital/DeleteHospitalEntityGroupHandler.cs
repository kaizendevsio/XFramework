using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class DeleteHospitalEntityGroupHandler : CommandBaseHandler, IRequestHandler<DeleteHospitalEntityGroupCmd, CmdResponse<DeleteHospitalEntityGroupCmd>>
{
    public DeleteHospitalEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteHospitalEntityGroupCmd>> Handle(DeleteHospitalEntityGroupCmd request, CancellationToken cancellationToken)
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
        
        existingGroup.IsDeleted = true;
        existingGroup.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingGroup);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        return new()
        {
            Message = $"Hospital Entity Group with Guid {request.Guid} deleted successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}