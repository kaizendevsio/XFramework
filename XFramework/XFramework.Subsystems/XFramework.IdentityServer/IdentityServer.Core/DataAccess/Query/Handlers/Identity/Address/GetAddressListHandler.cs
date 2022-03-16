using IdentityServer.Core.DataAccess.Query.Entity.Identity.Address;
using IdentityServer.Domain.Generic.Contracts.Responses.Address;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity.Address;

public class GetAddressListHandler : QueryBaseHandler, IRequestHandler<GetAddressListQuery, QueryResponse<List<IdentityAddressEntityResponse>>>
{
   

    public GetAddressListHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<List<IdentityAddressEntityResponse>>> Handle(GetAddressListQuery request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}