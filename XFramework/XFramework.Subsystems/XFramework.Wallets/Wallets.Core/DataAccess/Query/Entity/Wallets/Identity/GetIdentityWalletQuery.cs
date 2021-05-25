using MediatR;
using Wallets.Core.DataAccess.Commands.Entity;
using Wallets.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Core.DataAccess.Query.Entity.Wallets.Identity
{
    public class GetIdentityWalletQuery : QueryBaseEntity, IRequest<QueryResponseBO<GetIdentityWalletContract>>
    {
        public long Id { get; set; }
        public string Code { get; set; }
    }
}