using MediatR;
using Wallets.Domain.Generic.Contracts.Responses;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Core.DataAccess.Query.Entity.Wallets.Identity
{
    public class CreateIdentityWalletDepositQuery : QueryBaseEntity, IRequest<QueryResponseBO<WalletDepositContract>>
    {
        public string GatewayName { get; set; }
        public string WalletTypeCode { get; set; }
        public decimal? Amount { get; set; }
        public string Cuid { get; set; }
        public string Remarks { get; set; }
    }
}