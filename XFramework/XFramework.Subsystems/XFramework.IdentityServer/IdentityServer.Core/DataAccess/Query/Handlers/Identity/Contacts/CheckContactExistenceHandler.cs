using IdentityServer.Core.DataAccess.Query.Entity.Identity.Contacts;
using IdentityServer.Domain.DataTransferObjects.Legacy;
using XFramework.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity.Contacts;

public class CheckContactExistenceHandler : QueryBaseHandler ,IRequestHandler<CheckContactExistenceQuery, QueryResponseBO<ExistenceResponse>>
{
    private readonly LegacyContext _legacyContext;

    public CheckContactExistenceHandler(IDataLayer dataLayer, LegacyContext legacyContext)
    {
        _legacyContext = legacyContext;
        _dataLayer = dataLayer;
    }
        
    public async Task<QueryResponseBO<ExistenceResponse>> Handle(CheckContactExistenceQuery request, CancellationToken cancellationToken)
    {
        var existing = await _dataLayer.TblIdentityContacts
            .AsNoTracking()
            .Where(i => i.Value == request.Value)
            .Where(i => i.Id != request.Cid)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing != null)
        {
            return new ()
            {
                Message = $"The contact type {(GenericContactType)existing.UcentitiesId} '{request.Value}' already exists",
                HttpStatusCode = HttpStatusCode.Conflict
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}