using HealthEssentials.Core.DataAccess.Query.Entity.Vendor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Vendor;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Vendor;

public class GetVendorHandler : QueryBaseHandler, IRequestHandler<GetVendorQuery, QueryResponse<VendorResponse>>
{
    public GetVendorHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    public async Task<QueryResponse<VendorResponse>> Handle(GetVendorQuery request, CancellationToken cancellationToken)
    {
        var vendor = await _dataLayer.HealthEssentialsContext.Vendors
            .Include(x => x.Entity)
            .ThenInclude(x => x.Group)
            .AsSplitQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Guid == $"{request.Guid}", CancellationToken.None);
        
        if (vendor is null)
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "Vendor not found",
                
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Vendor found",
            Response = vendor.Adapt<VendorResponse>()
        };
    }
}