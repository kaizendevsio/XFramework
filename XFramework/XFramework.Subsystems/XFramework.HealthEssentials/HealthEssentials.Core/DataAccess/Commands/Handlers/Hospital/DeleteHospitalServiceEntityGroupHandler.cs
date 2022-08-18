using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class DeleteHospitalServiceEntityGroupHandler : CommandBaseHandler, IRequestHandler<DeleteHospitalServiceEntityGroupCmd, CmdResponse<DeleteHospitalServiceEntityGroupCmd>>
{
    public DeleteHospitalServiceEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteHospitalServiceEntityGroupCmd>> Handle(DeleteHospitalServiceEntityGroupCmd request, CancellationToken cancellationToken)
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
        
        existingGroup.IsDeleted = true;
        existingGroup.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingGroup);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(cancellationToken);
        
        return new ()
        {
            Message = $"Hospital Service Entity Group with Guid {request.Guid} deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}