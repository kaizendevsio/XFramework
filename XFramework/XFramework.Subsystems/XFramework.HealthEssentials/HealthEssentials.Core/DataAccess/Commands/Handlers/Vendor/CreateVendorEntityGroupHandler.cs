using HealthEssentials.Domain.Generics.Contracts;

namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Vendor;

public class CreateVendorEntityGroupHandler : CommandBaseHandler, IRequestHandler<CreateVendorEntityGroupCmd, CmdResponse<CreateVendorEntityGroupCmd>>
{
    public CreateVendorEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateVendorEntityGroupCmd>> Handle(CreateVendorEntityGroupCmd request, CancellationToken cancellationToken)
    {
        var group = request.Adapt<VendorTypeGroup>();
        group.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";

        await _dataLayer.HealthEssentialsContext.VendorEntityGroups.AddAsync(group, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(group.Guid);
        return new()
        {
            Message = $"Vendor Entity Group {group.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}