using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class DeleteHospitalServiceHandler : CommandBaseHandler, IRequestHandler<DeleteHospitalServiceCmd, CmdResponse<DeleteHospitalServiceCmd>>
{
    public DeleteHospitalServiceHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteHospitalServiceCmd>> Handle(DeleteHospitalServiceCmd request, CancellationToken cancellationToken)
    {
        var existingService = await _dataLayer.HealthEssentialsContext.HospitalServices.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingService is null)
        {
            return new ()
            {
                Message = $"Hospital Service with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingService.IsDeleted = true;
        existingService.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingService);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(cancellationToken);
        
        return new ()
        {
            Message = $"Hospital Service with Guid {request.Guid} deleted successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}