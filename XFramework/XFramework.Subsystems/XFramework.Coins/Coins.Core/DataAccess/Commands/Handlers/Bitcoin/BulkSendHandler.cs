using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Coins.Core.DataAccess.Commands.Entity.Bitcoin;
using Coins.Core.Interfaces.Wrappers;
using MediatR;
using XFramework.Domain.Generic.BusinessObjects;

namespace Coins.Core.DataAccess.Commands.Handlers.Bitcoin
{
    public class BulkSendHandler : CommandBaseHandler, IRequestHandler<BulkSendCmd,CmdResponseBO<BulkSendCmd>>
    {
        public BulkSendHandler(IBtcBlockchainWrapper btcBlockchainWrapper)
        {
            _btcBlockchainWrapper = btcBlockchainWrapper;
        }
        public async Task<CmdResponseBO<BulkSendCmd>> Handle(BulkSendCmd request, CancellationToken cancellationToken)
        {
            await _btcBlockchainWrapper.SendToMany(request.TransactionList);

            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted
            };
        }
    }
}