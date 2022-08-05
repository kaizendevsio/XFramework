using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Doctor;

public class UpdateDoctorTagHandler : CommandBaseHandler, IRequestHandler<UpdateDoctorTagCmd, CmdResponse<UpdateDoctorTagCmd>>
{
    public UpdateDoctorTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<UpdateDoctorTagCmd>> Handle(UpdateDoctorTagCmd request, CancellationToken cancellationToken)
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
        var updatedDoctorTag = request.Adapt(existingDoctorTag);

        if (request.DoctorGuid is null)
        {
            var doctor = await _dataLayer.HealthEssentialsContext.Doctors.FirstOrDefaultAsync(x => x.Guid == $"{request.DoctorGuid}", CancellationToken.None);
            if (doctor is null)
            {
                return new ()
                {
                    Message = $"Doctor with Guid {request.DoctorGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedDoctorTag.Doctor = doctor;
        }

        if (request.TagGuid is null)
        {
            var tag = await _dataLayer.HealthEssentialsContext.Tags.FirstOrDefaultAsync(x => x.Guid == $"{request.TagGuid}", CancellationToken.None);
            if (tag is null)
            {
                return new ()
                {
                    Message = $"Tag with Guid {request.TagGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedDoctorTag.Tag = tag;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedDoctorTag);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Doctor Tag with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}