using IdentityServer.Core.DataAccess.Query.Entity.Identity.Contacts;
using XFramework.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Handlers.Identity.Contacts;

public class CheckContactExistenceHandler : QueryBaseHandler ,IRequestHandler<CheckContactExistenceQuery, QueryResponse<ExistenceResponse>>
{
    public CheckContactExistenceHandler(IDataLayer dataLayer)
    {
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
        var application = await GetApplication(request.RequestServer.ApplicationId);
        var existing = await _dataLayer.IdentityContacts
            .AsNoTracking()
            .Where(i => i.Value == request.Value)
            .Where(i => i.Guid != $"{request.Guid}")
            .Where(i => i.UserCredential.ApplicationId == application.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing != null)
        {
            return new ()
            {
                Message = $"The {(GenericContactType)existing.EntityId} '{request.Value}' already exists",
                HttpStatusCode = HttpStatusCode.Conflict
            };
        }
        
        var contactGroup = await _dataLayer.IdentityContactGroups.FirstOrDefaultAsync(i => i.Guid == $"{request.GroupGuid}", CancellationToken.None);
        if (contactGroup is null)
        {
            return new ()
            {
                Message = $"The contact group with guid '{request.GroupGuid}' does not exist",
                HttpStatusCode = HttpStatusCode.Conflict
            };
        }

        return new()
        {
            HttpStatusCode = HttpStatusCode.Accepted
        };
    }
}