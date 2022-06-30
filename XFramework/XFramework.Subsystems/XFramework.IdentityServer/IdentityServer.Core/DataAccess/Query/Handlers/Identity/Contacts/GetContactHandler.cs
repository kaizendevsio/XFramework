using IdentityServer.Core.DataAccess.Query.Entity.Identity.Contacts;
using IdentityServer.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity.Contacts;

public class GetContactHandler : QueryBaseHandler ,IRequestHandler<GetContactQuery, QueryResponse<ContactResponse>>
{
    public GetContactHandler(IDataLayer dataLayer)
    {
        _dataLayer = dataLayer;
    }
    
    public async Task<QueryResponse<ContactResponse>> Handle(GetContactQuery request, CancellationToken cancellationToken)
    {
        var entity = await _dataLayer.IdentityContacts
            .Include( i => i.Entity)
            .Include( i => i.Group)
            .AsNoTracking()
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Guid == $"{request.Guid}", cancellationToken);
           
        if (entity == null)
        {
            return new ()
            {
                Message = $"Contact with Guid {request.Guid} does not exist",
                HttpStatusCode = HttpStatusCode.NotFound
            };
        }

        return new ()
        {
            HttpStatusCode = HttpStatusCode.Accepted,
            Response = entity.Adapt<ContactResponse>()
        };
    }
}