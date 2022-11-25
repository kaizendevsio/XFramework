using HealthEssentials.Core.DataAccess.Query.Entity.Vendor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Vendor;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Vendor;

public class GetVendorEntityGroupListHandler : QueryBaseHandler, IRequestHandler<GetVendorEntityGroupListQuery, QueryResponse<List<VendorEntityGroupResponse>>>
{
    public GetVendorEntityGroupListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<VendorEntityGroupResponse>>> Handle(GetVendorEntityGroupListQuery request, CancellationToken cancellationToken)
    {
        var group = await _dataLayer.HealthEssentialsContext.VendorEntityGroups
            .Where(x => EF.Functions.ILike(x.Name, $"%{request.SearchField}%"))
            .OrderBy(x => x.Name)
            .Take(request.PageSize)
            .AsSplitQuery()
            .AsNoTracking()
            .ToListAsync(CancellationToken.None);
        
        if (!group.Any())
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
            Response = group.Adapt<List<VendorEntityGroupResponse>>()
        };
    }
}