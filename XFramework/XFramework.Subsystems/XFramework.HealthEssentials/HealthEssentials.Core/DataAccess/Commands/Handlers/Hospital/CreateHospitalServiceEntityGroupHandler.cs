using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class CreateHospitalServiceEntityGroupHandler : CommandBaseHandler, IRequestHandler<CreateHospitalServiceEntityGroupCmd, CmdResponse<CreateHospitalServiceEntityGroupCmd>>
{
    public CreateHospitalServiceEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateHospitalServiceEntityGroupCmd>> Handle(CreateHospitalServiceEntityGroupCmd request, CancellationToken cancellationToken)
    {
        var group = request.Adapt<HospitalServiceEntityGroup>();
        group.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        
        await _dataLayer.HealthEssentialsContext.HospitalServiceEntityGroups.AddAsync(group, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(group.Guid);
        return new()
        {
            Message = $"Hospital Service Entity Group {group.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            IsSuccess = true,
        };
    }
}