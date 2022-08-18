using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class DeleteHospitalTagHandler : CommandBaseHandler, IRequestHandler<DeleteHospitalTagCmd, CmdResponse<DeleteHospitalTagCmd>>
{
    public DeleteHospitalTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteHospitalTagCmd>> Handle(DeleteHospitalTagCmd request, CancellationToken cancellationToken)
    {
        var existingHospitalTag = await _dataLayer.HealthEssentialsContext.HospitalTags.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingHospitalTag is null)
        {
            return new ()
            {
                Message = $"Hospital tag with guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingHospitalTag.IsDeleted = true;
        existingHospitalTag.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingHospitalTag);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(cancellationToken);
        
        return new ()
        {
            Message = $"Hospital tag with guid {request.Guid} deleted successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}