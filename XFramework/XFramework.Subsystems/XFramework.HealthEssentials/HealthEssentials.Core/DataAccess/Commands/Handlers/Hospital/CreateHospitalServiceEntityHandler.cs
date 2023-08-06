using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class CreateHospitalServiceEntityHandler : CommandBaseHandler, IRequestHandler<CreateHospitalServiceEntityCmd, CmdResponse<CreateHospitalServiceEntityCmd>>
{
    public CreateHospitalServiceEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateHospitalServiceEntityCmd>> Handle(CreateHospitalServiceEntityCmd request, CancellationToken cancellationToken)
    {
        var group = await _dataLayer.HealthEssentialsContext.HospitalServiceEntityGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.GroupGuid}", CancellationToken.None);
        if (group is null)
        {
            return new ()
            {
                Message = $"Hospital Service Group with Guid {request.GroupGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var entity = request.Adapt<HospitalServiceType>();
        entity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        entity.Group = group;
        
        await _dataLayer.HealthEssentialsContext.HospitalServiceEntities.AddAsync(entity, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(entity.Guid);
        return new()
        {
            Message = $"Hospital Service Entity {entity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };

    }
}