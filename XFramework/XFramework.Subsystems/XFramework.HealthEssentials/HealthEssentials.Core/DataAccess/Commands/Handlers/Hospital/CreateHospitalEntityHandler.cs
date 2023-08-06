using HealthEssentials.Core.DataAccess.Commands.Entity.Hospital;
using HealthEssentials.Domain.Generics.Contracts;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Hospital;

public class CreateHospitalEntityHandler : CommandBaseHandler, IRequestHandler<CreateHospitalEntityCmd, CmdResponse<CreateHospitalEntityCmd>>
{
    public CreateHospitalEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreateHospitalEntityCmd>> Handle(CreateHospitalEntityCmd request, CancellationToken cancellationToken)
    {
        var group = await _dataLayer.HealthEssentialsContext.HospitalEntityGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.GroupGuid}", CancellationToken.None);
        if (group is null)
        {
            return new ()
            {
                Message = $"Hospital Entity Group with Guid {request.GroupGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var entity = request.Adapt<HospitalType>();
        entity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        entity.Group = group;
        
        await _dataLayer.HealthEssentialsContext.HospitalEntities.AddAsync(entity, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(entity.Guid);
        return new()
        {
            Message = $"Hospital Entity {entity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}