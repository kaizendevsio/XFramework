using IdentityServer.Domain.Generic.Contracts.Requests.Check;
using XFramework.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Contacts;

public class CheckContactExistenceQuery : CheckContactExistenceRequest, IRequest<QueryResponse<ExistenceResponse>>
{
    
}