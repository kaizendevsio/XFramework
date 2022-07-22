using HealthEssentials.Core.DataAccess.Query.Entity.Vendor;
using HealthEssentials.Domain.Generics.Contracts.Responses.Vendor;

namespace HealthEssentials.Core.DataAccess.Query.Handlers.Vendor;

public class GetVendorHandler : QueryBaseHandler, IRequestHandler<GetVendorQuery, QueryResponse<VendorResponse>>
{
    public GetVendorHandler()
    {
        
    }
    public async Task<QueryResponse<VendorResponse>> Handle(GetVendorQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}