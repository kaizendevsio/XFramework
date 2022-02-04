using XFramework.Domain.Generic.Contracts.Responses;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Contacts;

public class CheckContactExistenceQuery : QueryBaseEntity, IRequest<QueryResponseBO<ExistenceResponse>>
{
    public GenericContactType ContactType { get; set; }
    public string Value { get; set; }
    public long Cid { get; set; }
}