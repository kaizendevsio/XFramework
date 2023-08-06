using HealthEssentials.Core.DataAccess.Commands.Entity.Pharmacy;
using HealthEssentials.Domain.Generics.Contracts;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Pharmacy;

public class CreatePharmacyServiceEntityHandler : CommandBaseHandler, IRequestHandler<CreatePharmacyServiceEntityCmd, CmdResponse<CreatePharmacyServiceEntityCmd>>
{
    public CreatePharmacyServiceEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }

    public async Task<CmdResponse<CreatePharmacyServiceEntityCmd>> Handle(CreatePharmacyServiceEntityCmd request, CancellationToken cancellationToken)
    {
        var group = await _dataLayer.HealthEssentialsContext.PharmacyServiceEntityGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.GroupGuid}", CancellationToken.None);
        if (group is null)
        {
            return new ()
            {
                Message = $"Group with Guid {request.GroupGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var entity = request.Adapt<PharmacyServiceType>();
        entity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        entity.Group = group;
        
        await _dataLayer.HealthEssentialsContext.PharmacyServiceEntities.AddAsync(entity, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(entity.Guid);
        return new()
        {
            Message = $"Pharmacy Service Entity with Guid {entity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}