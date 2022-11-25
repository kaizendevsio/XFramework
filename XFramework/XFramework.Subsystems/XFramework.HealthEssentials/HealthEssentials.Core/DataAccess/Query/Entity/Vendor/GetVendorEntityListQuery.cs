using HealthEssentials.Domain.Generics.Contracts.Requests.Vendor.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Vendor;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Vendor;

public class GetVendorEntityListQuery : GetVendorEntityListRequest, IRequest<QueryResponse<List<VendorEntityResponse>>>
{
    
}