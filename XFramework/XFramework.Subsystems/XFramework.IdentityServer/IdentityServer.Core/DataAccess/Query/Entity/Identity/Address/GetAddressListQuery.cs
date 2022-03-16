using IdentityServer.Domain.Generic.Contracts.Requests.Get.Address;
using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Address;

public class GetAddressListQuery : GetAddressListRequest, IRequest<QueryResponse<List<IdentityAddressEntityResponse>>>
{
    
}