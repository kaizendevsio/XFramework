using HealthEssentials.Core.DataAccess.Commands.Entity.Patient;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Patient;

public class UpdatePatientTagHandler : CommandBaseHandler, IRequestHandler<UpdatePatientTagCmd, CmdResponse<UpdatePatientTagCmd>>
{
    public UpdatePatientTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdatePatientTagCmd>> Handle(UpdatePatientTagCmd request, CancellationToken cancellationToken)
    {
        var existingTag = await _dataLayer.HealthEssentialsContext.PatientTags.FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        if (existingTag is null)
        {
            return new ()
            {
                Message = $"Patient tag with guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }
        var updatedTag = request.Adapt(existingTag);

        if (request.PatientGuid is null)
        {
            var patient = await _dataLayer.HealthEssentialsContext.Patients.FirstOrDefaultAsync(x => x.Guid == $"{request.PatientGuid}", CancellationToken.None);
            if (patient is null)
            {
                return new ()
                {
                    Message = $"Patient with Guid {request.PatientGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedTag.Patient = patient;
        }

        if (request.TagGuid is null)
        {
            var tag = await _dataLayer.HealthEssentialsContext.Tags.FirstOrDefaultAsync(x => x.Guid == $"{request.TagGuid}", CancellationToken.None);
            if (tag is null)
            {
                return new ()
                {
                    Message = $"Tag with Guid {request.TagGuid} does not exist",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedTag.Tag = tag;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedTag);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        return new ()
        {
            Message = $"Patient tag with guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}