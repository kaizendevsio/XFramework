using IdentityServer.Domain.Generic.Contracts.Requests.Check;
using XFramework.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Credentials;

public class CheckCredentialExistenceQuery : CheckCredentialExistenceRequest, IRequest<QueryResponseBO<ExistenceResponse>>
{

}