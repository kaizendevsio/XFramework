using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Core.DataAccess.Commands.Entity.Wallets
{
    public class DeleteWalletCmd : CommandBaseEntity, IRequest<CmdResponseBO<DeleteWalletCmd>>
    {
        public long Id { get; set; }
    }
}