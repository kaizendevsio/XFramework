using HealthEssentials.Core.DataAccess.Query.Entity.Vendor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Vendor;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Vendor;

public class GetVendorEntityGroupHandler : QueryBaseHandler, IRequestHandler<GetVendorEntityGroupQuery, QueryResponse<VendorEntityGroupResponse>>
{
    public GetVendorEntityGroupHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<VendorEntityGroupResponse>> Handle(GetVendorEntityGroupQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}