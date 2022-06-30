using IdentityServer.Domain.Generic.Contracts.Requests.Get.Address;
using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace IdentityServer.Core.DataAccess.Query.Entity.Address;

public class GetProvinceListQuery : GetProvinceListRequest, IRequest<QueryResponse<List<AddressProvinceResponse>>>
{
    
}