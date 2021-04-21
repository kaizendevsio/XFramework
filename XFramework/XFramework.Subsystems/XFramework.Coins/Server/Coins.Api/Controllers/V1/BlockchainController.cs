using System.Collections.Generic;
using System.Threading.Tasks;
using Coins.Core.DataAccess.Commands.Entity.Bitcoin;
using Coins.Domain.BusinessObjects;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Coins.Api.Controllers.V1
{
    [Route("[controller]")]
    [ApiController]
    public class BlockchainController : ControllerBase
    {
        private IMediator Mediator { get; set; }
        public BlockchainController(IMediator mediator)
        {
            Mediator = mediator;
        }
        
        [HttpPost("Send")]
        public async Task<JsonResult> Post(List<BtcTransactionBO> transactionList)
        {
           var request = new BulkSendCmd() {TransactionList = transactionList};
           var cmdResponseBo = await Mediator.Send(request);
           return new JsonResult(cmdResponseBo);
        }
    }
}