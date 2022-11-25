using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class DeleteHospitalServiceTagHandler : CommandBaseHandler, IRequestHandler<DeleteHospitalServiceTagCmd, CmdResponse<DeleteHospitalServiceTagCmd>>
{
    public DeleteHospitalServiceTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<DeleteHospitalServiceTagCmd>> Handle(DeleteHospitalServiceTagCmd request, CancellationToken cancellationToken)
    {
        var existingServiceTag = await _dataLayer.HealthEssentialsContext.HospitalServiceTags.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingServiceTag is null)
        {
            return new ()
            {
                Message = $"Hospital Service Tag with Guid {request.Guid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingServiceTag.IsDeleted = true;
        existingServiceTag.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingServiceTag);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Hospital Service Tag with Guid {request.Guid} deleted successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}