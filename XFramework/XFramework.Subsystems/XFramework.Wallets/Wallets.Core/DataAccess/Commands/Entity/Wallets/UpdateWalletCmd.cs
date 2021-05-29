using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Core.DataAccess.Commands.Entity.Wallets
{
    public class UpdateWalletCmd  : CommandBaseEntity, IRequest<CmdResponseBO<UpdateWalletCmd>>
    {
        public long Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string Desc { get; set; }
        public short Type { get; set; }
        public long? CurrencyEntityId { get; set; }
        public decimal? MinTransfer { get; set; }
        public decimal? MaxTransfer { get; set; }
    }
}