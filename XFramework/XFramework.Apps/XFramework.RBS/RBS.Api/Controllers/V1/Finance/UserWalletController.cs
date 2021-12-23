using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using RBS.Core.DataAccess.Commands.Entity.Wallet.User;
using RBS.Core.DataAccess.Query.Entity.Wallet.User;

namespace RBS.Api.Controllers.V1.Finance
{
    [Route("Api/V1/Wallet/User")]
    [ApiController]
    public class UserWalletController : XFrameworkControllerBase
    {
        [HttpPost("Deposit")]
        public async Task<JsonResult> Deposit(WalletDepositCmd request)
        {
            var result = await _mediator.Send(request);
            return new(result);
        }
        
        [HttpPost("Withdraw")]
        public async Task<JsonResult> Withdraw(WalletWithdrawCmd request)
        {
            var result = await _mediator.Send(request);
            return new(result);
        }
        
        [HttpPost("Transfer")]
        public async Task<JsonResult> Transfer(WalletTransferCmd request)
        {
            var result = await _mediator.Send(request);
            return new(result);
        }
        
        [HttpGet]
        public async Task<JsonResult> Get(Guid guid, string userName, int walletId)
        {
            var request = new WalletDetailsQuery
            {
                Uid = guid,
                UserName = userName,
                WalletId = walletId
            };
            var result = await _mediator.Send(request);
            return new(result);
        }
        
        [HttpGet("Transactions")]
        public async Task<JsonResult> Transactions(Guid guid, string userName, int walletId)
        {
            var request = new WalletTransactionsQuery()
            {
                Uid = guid,
                UserName = userName,
                WalletId = walletId
            };   
            var result = await _mediator.Send(request);
            return new(result);
        }
    }
}