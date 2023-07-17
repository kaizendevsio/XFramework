using HealthEssentials.Core.DataAccess.Commands.Entity.Doctor;
using HealthEssentials.Domain.DataTransferObjects.XnelSystemsHealthEssentials;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Doctor;

public class CreateDoctorEntityGroupHandler : CommandBaseHandler, IRequestHandler<CreateDoctorEntityGroupCmd, CmdResponse<CreateDoctorEntityGroupCmd>>
{
    public CreateDoctorEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateDoctorEntityGroupCmd>> Handle(CreateDoctorEntityGroupCmd request, CancellationToken cancellationToken)
    {
        var group = request.Adapt<DoctorEntityGroup>();
        group.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";

        await _dataLayer.HealthEssentialsContext.DoctorEntityGroups.AddAsync(group, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(group.Guid);
        return new()
        {
            Message = $"Doctor Entity Group with Id {group.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}