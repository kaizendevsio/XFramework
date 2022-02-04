using XFramework.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Credential;

public class CheckCredentialExistenceQuery : QueryBaseEntity, IRequest<QueryResponseBO<ExistenceResponse>>
{
    public string UserName { get; set; }
    public string Cuid { get; set; }
}