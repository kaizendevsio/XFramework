using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Doctor;

public class CreateDoctorTagHandler : CommandBaseHandler, IRequestHandler<CreateDoctorTagCmd, CmdResponse<CreateDoctorTagCmd>>
{
    public CreateDoctorTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateDoctorTagCmd>> Handle(CreateDoctorTagCmd request, CancellationToken cancellationToken)
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
        
        var tag = await _dataLayer.HealthEssentialsContext.Tags.FirstOrDefaultAsync(x => x.Guid == $"{request.TagGuid}", CancellationToken.None);
        if (tag is null)
        {
            return new ()
            {
                Message = $"Tag with Guid {request.TagGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var doctorTag = request.Adapt<DoctorTag>();
        doctorTag.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        doctorTag.Doctor = doctor;
        doctorTag.Tag = tag;
        
        await _dataLayer.HealthEssentialsContext.DoctorTags.AddAsync(doctorTag, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(doctorTag.Guid);
        return new()
        {
            Message = $"Doctor Tag with Guid {doctorTag.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}