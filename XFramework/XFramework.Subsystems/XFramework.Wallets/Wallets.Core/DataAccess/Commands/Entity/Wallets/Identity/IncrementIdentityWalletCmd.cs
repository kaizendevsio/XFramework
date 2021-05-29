using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity
{
    public class IncrementIdentityWalletCmd  : CommandBaseEntity, IRequest<CmdResponseBO<IncrementIdentityWalletCmd>>
    {
        public long UserCredentialId { get; set; }
        public long? WalletTypeId { get; set; }
        public decimal? Amount { get; set; }
    }
}