using IdentityServer.Domain.Generic.Contracts.Requests.Get.Address;
using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace IdentityServer.Core.DataAccess.Query.Entity.Address;

public class GetAddressEntityListQuery : GetAddressEntityListRequest, IRequest<QueryResponse<List<AddressCountryResponse>>>
{
    
}