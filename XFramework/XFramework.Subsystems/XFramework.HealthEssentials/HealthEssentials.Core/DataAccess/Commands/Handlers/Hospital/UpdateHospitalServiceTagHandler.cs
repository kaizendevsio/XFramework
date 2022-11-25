using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class UpdateHospitalServiceTagHandler : CommandBaseHandler, IRequestHandler<UpdateHospitalServiceTagCmd, CmdResponse<UpdateHospitalServiceTagCmd>>
{
    public UpdateHospitalServiceTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<UpdateHospitalServiceTagCmd>> Handle(UpdateHospitalServiceTagCmd request, CancellationToken cancellationToken)
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
        var updatedServiceTag = request.Adapt(existingServiceTag);

        if (request.HospitalServiceGuid is null)
        {
            var hospitalService = await _dataLayer.HealthEssentialsContext.HospitalServices.FirstOrDefaultAsync(x => x.Guid == $"{request.HospitalServiceGuid}", CancellationToken.None);
            if (hospitalService is null)
            {
                return new ()
                {
                    Message = $"Hospital Service with Guid {request.HospitalServiceGuid} not found",
                    HttpStatusCode = HttpStatusCode.NotFound
                };
            }
            updatedServiceTag.HospitalService = hospitalService;
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
            updatedServiceTag.Tag = tag;
        }

        _dataLayer.HealthEssentialsContext.Update(updatedServiceTag);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(cancellationToken);
        
        return new ()
        {
            Message = $"Hospital Service Tag with Guid {request.Guid} updated successfully",
            HttpStatusCode = HttpStatusCode.OK
        };

    }
}