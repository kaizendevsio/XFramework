using HealthEssentials.Core.DataAccess.Query.Entity.Vendor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Vendor;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Vendor;

public class GetVendorListHandler : QueryBaseHandler, IRequestHandler<GetVendorListQuery, QueryResponse<List<VendorResponse>>>
{
    public GetVendorListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<VendorResponse>>> Handle(GetVendorListQuery request, CancellationToken cancellationToken)
    {
        var vendor = await _dataLayer.HealthEssentialsContext.Vendors
            .Include(x => x.Entity)
            .ThenInclude(x => x.Group)
            .Where(x => EF.Functions.ILike(x.Name, $"%{request.SearchField}%"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
            
        if (!vendor.Any())
        {
            return new()
            {
                HttpStatusCode = HttpStatusCode.NoContent,
                Message = "No records found",
                IsSuccess = true
            };
        }
        
        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Message = "Records found",
            IsSuccess = true,
            Response = vendor.Adapt<List<VendorResponse>>()
        };
    }
}