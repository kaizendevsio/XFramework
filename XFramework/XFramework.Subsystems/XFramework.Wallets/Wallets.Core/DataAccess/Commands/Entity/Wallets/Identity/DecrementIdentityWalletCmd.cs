using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity
{
    public class DecrementIdentityWalletCmd : CommandBaseEntity, IRequest<CmdResponseBO<DecrementIdentityWalletCmd>>
    {
        public long UserAuthId { get; set; }
        public long? WalletTypeId { get; set; }
        public decimal? Amount { get; set; }
    }
}