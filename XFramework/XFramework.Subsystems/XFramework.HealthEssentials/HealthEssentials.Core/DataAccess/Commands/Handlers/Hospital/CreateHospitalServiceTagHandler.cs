using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class CreateHospitalServiceTagHandler : CommandBaseHandler, IRequestHandler<CreateHospitalServiceTagCmd, CmdResponse<CreateHospitalServiceTagCmd>>
{
    public CreateHospitalServiceTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateHospitalServiceTagCmd>> Handle(CreateHospitalServiceTagCmd request, CancellationToken cancellationToken)
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
        
        var tag = await _dataLayer.HealthEssentialsContext.Tags.FirstOrDefaultAsync(x => x.Guid == $"{request.TagGuid}", CancellationToken.None);
        if (tag is null)
        {
            return new ()
            {
                Message = $"Tag with Guid {request.TagGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var serviceTag = request.Adapt<HospitalServiceTag>();
        serviceTag.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        serviceTag.HospitalService = hospitalService;
        serviceTag.Tag = tag;
        
        await _dataLayer.HealthEssentialsContext.HospitalServiceTags.AddAsync(serviceTag, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(serviceTag.Guid);
        return new()
        {
            Message = $"Hospital Service Tag with Guid {serviceTag.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}