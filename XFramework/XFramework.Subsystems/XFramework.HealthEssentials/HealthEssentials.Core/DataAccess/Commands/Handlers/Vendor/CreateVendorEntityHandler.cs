using HealthEssentials.Domain.Generics.Contracts;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Vendor;

public class CreateVendorEntityHandler : CommandBaseHandler, IRequestHandler<CreateVendorEntityCmd, CmdResponse<CreateVendorEntityCmd>>
{
    public CreateVendorEntityHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateVendorEntityCmd>> Handle(CreateVendorEntityCmd request, CancellationToken cancellationToken)
    {
        var group = await _dataLayer.HealthEssentialsContext.VendorEntityGroups.FirstOrDefaultAsync(x => x.Guid == $"{request.GroupGuid}", CancellationToken.None);
        if (group is null)
        {
            return new ()
            {
                Message = $"Entity group with guid {request.GroupGuid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var entity = request.Adapt<VendorType>();
        entity.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        entity.Group = group;
        
        await _dataLayer.HealthEssentialsContext.VendorEntities.AddAsync(entity, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);

        request.Guid = Guid.Parse(entity.Guid);
        return new()
        {
            Message = $"Entity {entity.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}