using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity
{
    public class DeleteIdentityWalletCmd : CommandBaseEntity, IRequest<CmdResponseBO<DeleteIdentityWalletCmd>>
    {
        public string Cuid { get; set; }
        public long? WalletTypeId { get; set; }
    }
}