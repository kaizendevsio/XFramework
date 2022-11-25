using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class DeleteHospitalHandler : CommandBaseHandler, IRequestHandler<DeleteHospitalCmd, CmdResponse<DeleteHospitalCmd>>
{
    public DeleteHospitalHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteHospitalCmd>> Handle(DeleteHospitalCmd request, CancellationToken cancellationToken)
    {
        var existingHospital = await _dataLayer.HealthEssentialsContext.Hospitals.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingHospital is null)
        {
            return new ()
            {
                Message = $"Hospital with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingHospital.IsDeleted = true;
        existingHospital.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingHospital);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(cancellationToken);
        
        return new ()
        {
            Message = $"Hospital with Guid {request.Guid} deleted successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}