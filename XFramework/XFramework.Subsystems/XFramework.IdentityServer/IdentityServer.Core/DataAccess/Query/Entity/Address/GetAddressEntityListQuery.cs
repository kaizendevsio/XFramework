using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace IdentityServer.Core.DataAccess.Query.Entity.Address;

public class GetAddressEntityListQuery : QueryBaseEntity, IRequest<QueryResponse<List<AddressCountryResponse>>>
{
    
}