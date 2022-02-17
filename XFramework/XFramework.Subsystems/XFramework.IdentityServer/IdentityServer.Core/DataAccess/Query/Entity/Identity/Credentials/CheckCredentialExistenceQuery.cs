using XFramework.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Credentials;

public class CheckCredentialExistenceQuery : QueryBaseEntity, IRequest<QueryResponseBO<ExistenceResponse>>
{
    public string UserName { get; set; }
    public string Cuid { get; set; }
    public string PasswordString { get; set; }
}