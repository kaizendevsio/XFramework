using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Doctor;

public class DeleteDoctorTagHandler : CommandBaseHandler, IRequestHandler<DeleteDoctorTagCmd, CmdResponse<DeleteDoctorTagCmd>>
{
    public DeleteDoctorTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<DeleteDoctorTagCmd>> Handle(DeleteDoctorTagCmd request, CancellationToken cancellationToken)
    {
        var existingDoctorTag = await _dataLayer.HealthEssentialsContext.DoctorTags.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingDoctorTag is null)
        {
            return new ()
            {
                Message = $"Doctor Tag with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        
        existingDoctorTag.IsDeleted = true;
        existingDoctorTag.IsEnabled = false;

        _dataLayer.HealthEssentialsContext.Update(existingDoctorTag);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Doctor Tag with Guid {request.Guid} has been deleted",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}