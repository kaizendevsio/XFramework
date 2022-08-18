using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class UpdateHospitalTagHandler : CommandBaseHandler, IRequestHandler<UpdateHospitalTagCmd, CmdResponse<UpdateHospitalTagCmd>>
{
    public UpdateHospitalTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateHospitalTagCmd>> Handle(UpdateHospitalTagCmd request, CancellationToken cancellationToken)
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
        var updatedHospitalTag = request.Adapt(existingHospitalTag);

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
            updatedHospitalTag.Tag = tag;
        }

        if (request.HospitalGuid is null)
        {
            var hospital = await _dataLayer.HealthEssentialsContext.Hospitals.FirstOrDefaultAsync(x => x.Guid == $"{request.HospitalGuid}", CancellationToken.None);
            if (hospital is null)
            {
                return new ()
                {
                    Message = $"Hospital with Guid {request.HospitalGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedHospitalTag.Hospital = hospital;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedHospitalTag);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(cancellationToken);
        
        return new ()
        {
            Message = $"Hospital tag with guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };
    }
}