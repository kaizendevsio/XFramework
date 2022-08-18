using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class DeleteHospitalServiceEntityHandler : CommandBaseHandler, IRequestHandler<DeleteHospitalServiceEntityCmd, CmdResponse<DeleteHospitalServiceEntityCmd>>
{
    public DeleteHospitalServiceEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteHospitalServiceEntityCmd>> Handle(DeleteHospitalServiceEntityCmd request, CancellationToken cancellationToken)
    {
        var existingEntity = await _dataLayer.HealthEssentialsContext.HospitalServiceEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingEntity is null)
        {
            return new ()
            {
                Message = $"Hospital Service Entity with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingEntity.IsDeleted = true;
        existingEntity.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingEntity);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Hospital Service Entity with Guid {request.Guid} deleted successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}