using MediatR;
using XFramework.Domain.Generic.BusinessObjects;
using XFramework.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.Enums;

namespace IdentityServer.Core.DataAccess.Query.Entity.Identity.Contacts
{
    public class CheckContactExistenceQuery : QueryBaseEntity, IRequest<QueryResponseBO<ExistenceContract>>
    {
        public GenericContactType ContactType { get; set; }
        public string Value { get; set; }
        public long Cid { get; set; }
    }
}