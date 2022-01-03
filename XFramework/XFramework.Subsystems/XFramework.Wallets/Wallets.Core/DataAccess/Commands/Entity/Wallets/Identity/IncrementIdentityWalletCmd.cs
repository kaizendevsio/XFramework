using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity
{
    public class IncrementIdentityWalletCmd  : CommandBaseEntity, IRequest<CmdResponseBO<IncrementIdentityWalletCmd>>
    {
        public string Cuid { get; set; }
        public long? WalletTypeId { get; set; }
        public decimal? Amount { get; set; }
    }
}