using System.Collections.Generic;
using Coins.Domain.BusinessObjects;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace Coins.Core.DataAccess.Commands.Entity.Bitcoin
{
    public class BulkSendCmd : CommandBaseEntity, IRequest<CmdResponseBO<BulkSendCmd>>
    {
        public List<BtcTransactionBO> TransactionList { get; set; }
    }
}