using IdentityServer.Domain.Generic.Contracts.Requests.Check;
using XFramework.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity;

public class CheckIdentityExistenceQuery : CheckIdentityExistenceRequest, IRequest<QueryResponseBO<ExistenceResponse>>
{
  
}