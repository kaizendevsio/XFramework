using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class CreateHospitalTagHandler : CommandBaseHandler, IRequestHandler<CreateHospitalTagCmd, CmdResponse<CreateHospitalTagCmd>>
{
    public CreateHospitalTagHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateHospitalTagCmd>> Handle(CreateHospitalTagCmd request, CancellationToken cancellationToken)
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
        
        var hospital = await _dataLayer.HealthEssentialsContext.Hospitals.FirstOrDefaultAsync(x => x.Guid == $"{request.HospitalGuid}", CancellationToken.None);
        if (hospital is null)
        {
            return new ()
            {
                Message = $"Hospital with Guid {request.HospitalGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var hospitalTag = request.Adapt<HospitalTag>();
        hospitalTag.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        hospitalTag.Hospital = hospital;
        hospitalTag.Tag = tag;
        
        await _dataLayer.HealthEssentialsContext.HospitalTags.AddAsync(hospitalTag, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
           
        request.Guid = Guid.Parse(hospitalTag.Guid);
        return new()
        {
            Message = $"Hospital Tag with Guid {hospitalTag.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}