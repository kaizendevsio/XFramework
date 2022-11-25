using HealthEssentials.Domain.Generics.Contracts.Requests.Vendor.Get;
using HealthEssentials.Domain.Generics.Contracts.Responses.Vendor;

namespace HealthEssentials.Core.DataAccess.Query.Entity.Vendor;

public class GetVendorEntityQuery : GetVendorEntityRequest, IRequest<QueryResponse<VendorEntityResponse>>
{
    
}