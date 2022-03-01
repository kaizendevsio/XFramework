using IdentityServer.Core.DataAccess.Query.Entity.Identity.Contacts;
using IdentityServer.Domain.DataTransferObjects.Legacy;
using XFramework.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity.Contacts;

public class CheckContactExistenceHandler : QueryBaseHandler ,IRequestHandler<CheckContactExistenceQuery, QueryResponse<ExistenceResponse>>
{
    private readonly LegacyContext _legacyContext;

    public CheckContactExistenceHandler(IDataLayer dataLayer, LegacyContext legacyContext)
    {
        _legacyContext = legacyContext;
        _dataLayer = dataLayer;
    }
        
    public async Task<QueryResponse<ExistenceResponse>> Handle(CheckContactExistenceQuery request, CancellationToken cancellationToken)
    {
        switch (request.ContactType)
        {
            case GenericContactType.NotSpecified:
                break;
            case GenericContactType.Email:
                request.Value.ValidateEmailAddress();
                break;
            case GenericContactType.Phone:
                request.Value = request.Value.ValidatePhoneNumber();
                break;
        }
        
        var existing = await _dataLayer.TblIdentityContacts
            .AsNoTracking()
            .Where(i => i.Value == request.Value)
            .Where(i => i.Guid != $"{request.Guid}")
            .FirstOrDefaultAsync(cancellationToken);
        if (existing != null)
        {
            return new ()
            {
                Message = $"The {(GenericContactType)existing.UcentitiesId} '{request.Value}' already exists",
                HttpStatusCode = HttpStatusCode.Conflict
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}