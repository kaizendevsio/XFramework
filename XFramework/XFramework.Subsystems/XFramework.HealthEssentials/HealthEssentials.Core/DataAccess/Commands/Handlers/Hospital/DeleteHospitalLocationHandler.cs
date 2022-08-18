using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class DeleteHospitalLocationHandler : CommandBaseHandler, IRequestHandler<DeleteHospitalLocationCmd, CmdResponse<DeleteHospitalLocationCmd>>
{
    public DeleteHospitalLocationHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteHospitalLocationCmd>> Handle(DeleteHospitalLocationCmd request, CancellationToken cancellationToken)
    {
        var existingHospitalLocation = await _dataLayer.HealthEssentialsContext.HospitalLocations.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingHospitalLocation is null)
        {
            return new ()
            {
                Message = $"Hospital Location with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingHospitalLocation.IsDeleted = true;
        existingHospitalLocation.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingHospitalLocation);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(cancellationToken);
        
        return new ()
        {
            Message = $"Hospital Location with Guid {request.Guid} deleted successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}