using MediatR;
using Wallets.Core.DataAccess.Commands.Entity;
using Wallets.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Core.DataAccess.Query.Entity.Wallets
{
    public class GetWalletQuery : CommandBaseEntity, IRequest<QueryResponseBO<GetWalletContract>>
     {
        public long Id { get; set; }
        public string Code { get; set; }
    }
}