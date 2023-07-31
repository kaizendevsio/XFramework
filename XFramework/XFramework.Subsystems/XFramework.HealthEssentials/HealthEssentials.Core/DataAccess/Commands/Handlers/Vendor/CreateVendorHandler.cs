namespace HealthEssentials.Core.DataAccess.Commands.Handlers.Vendor;

public class CreateVendorHandler : CommandBaseHandler, IRequestHandler<CreateVendorCmd, CmdResponse<CreateVendorCmd>>
{
    public CreateVendorHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<CmdResponse<CreateVendorCmd>> Handle(CreateVendorCmd request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.HealthEssentialsContext.VendorEntities.FirstOrDefaultAsync(x => x.Guid == $"{request.EntityGuid}", CancellationToken.None);
        if (entity is null)
        {
            return new ()
            {
                Message = $"Entity with Guid {request.EntityGuid} not found",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        var vendor = request.Adapt<Domain.DataTransferObjects.XnelSystemsHealthEssentials.Vendor>();
        vendor.Guid = request.Guid is null ? $"{Guid.NewGuid()}" : $"{request.Guid}";
        vendor.Entity = entity;
        
        await _dataLayer.HealthEssentialsContext.Vendors.AddAsync(vendor, CancellationToken.None);
        await _dataLayer.HealthEssentialsContext.SaveChangesAsync(CancellationToken.None);
        
        request.Guid = Guid.Parse(vendor.Guid);
        return new()
        {
            Message = $"Vendor with Guid {vendor.Guid} created successfully",
            HttpStatusCode = HttpStatusCode.Accepted,
            
        };
    }
}