using System.Collections.Generic;
using System.Threading.Tasks;
using Coins.Core.Services;
using Coins.Domain.BusinessObjects;
using Microsoft.AspNetCore.Mvc;

namespace Coins.Api.Controllers.V1
{
    [Route("[controller]")]
    [ApiController]
    public class BlockchainController : ControllerBase
    {
        private readonly IBlockchainService _blockchainService;

        public BlockchainController(IBlockchainService blockchainService)
        {
            _blockchainService = blockchainService;
        }
        
        [HttpPost("Send")]
        public async Task<JsonResult> Post(List<BtcTransactionBO> transactionList)
        {
            var response = await _blockchainService.BulkSendAsync(transactionList);
            return new JsonResult(response);
        }
    }
}