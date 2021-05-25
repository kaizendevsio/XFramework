using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Wallets.Domain.Generic.Contracts.Requests.Wallet;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Api.Controllers.V1.Wallets
{
    [Route("API/v1/[controller]")]
    [ApiController]
    public class WalletsController : XFrameworkControllerBase
    {
        public IWalletServiceWrapper WalletServiceWrapper { get; }

        public WalletsController(IWalletServiceWrapper walletServiceWrapper)
        {
            WalletServiceWrapper = walletServiceWrapper;
        }

        [HttpPost("Create")]
        public async Task<JsonResult> Create([FromBody] CreateWalletRequest request)
        {
            var result = await WalletServiceWrapper.CreateWallet(request);
            return new JsonResult(result);
        }

        [HttpPost("Get")]
        public async Task<JsonResult> Get([FromBody] GetWalletRequest request)
        {
            var result = await WalletServiceWrapper.GetWallet(request);
            return new JsonResult(result);
        }

        [HttpPost("GetAll")]
        public async Task<JsonResult> GetAll([FromBody] GetAllWalletRequest request)
        {
            var result = await WalletServiceWrapper.GetAllWallet(request);
            return new JsonResult(result);
        }

        [HttpPost("Update")]
        public async Task<JsonResult> Update([FromBody] UpdateWalletRequest request)
        {
            var result = await WalletServiceWrapper.UpdateWallet(request);
            return new JsonResult(result);
        }

        [HttpPost("Delete")]
        public async Task<JsonResult> Delete([FromBody] DeleteWalletRequest request)
        {
            var result = await WalletServiceWrapper.DeleteWallet(request);
            return new JsonResult(result);
        }

     
    }
}