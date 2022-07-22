using HealthEssentials.Core.DataAccess.Query.Entity.Vendor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Vendor;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Vendor;

public class GetVendorEntityHandler : QueryBaseHandler, IRequestHandler<GetVendorEntityQuery, QueryResponse<VendorEntityResponse>>
{
    public GetVendorEntityHandler()
    {
        
    }
    public async Task<QueryResponse<VendorEntityResponse>> Handle(GetVendorEntityQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}