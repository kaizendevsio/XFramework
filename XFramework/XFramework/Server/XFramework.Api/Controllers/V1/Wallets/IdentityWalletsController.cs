using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wallets.Domain.Generic.Contracts.Requests.Create;
using Wallets.Domain.Generic.Contracts.Requests.Delete;
using Wallets.Domain.Generic.Contracts.Requests.Get;
using Wallets.Domain.Generic.Contracts.Requests.Update;

namespace XFramework.Api.Controllers.V1.Wallets
{
    [Authorize]
    [Route("Api/v{version:apiVersion}/[controller]")]
    [Route("Api/[controller]")]
    [ApiController]
    [ApiVersion("1.0")]
    public class IdentityWalletsController : XFrameworkControllerBase
    {
        public IWalletServiceWrapper WalletServiceWrapper { get; }

        public IdentityWalletsController(IWalletServiceWrapper walletServiceWrapper)
        {
            WalletServiceWrapper = walletServiceWrapper;
        }

        [HttpPost("Create")]
        public async Task<JsonResult> Create([FromBody] CreateWalletRequest request)
        {
            var result = await WalletServiceWrapper.CreateWallet(InjectAuthorization(request));
            return new JsonResult(result);
        }

        [HttpGet("Get")]
        public async Task<JsonResult> Get(Guid? guid)
        {
            var request = new GetWalletRequest() {Guid = guid};
            var result = await WalletServiceWrapper.GetWallet(request);
            return new JsonResult(result);
        }

        [HttpGet("GetAll")]
        public async Task<JsonResult> GetAll(Guid? credentialGuid)
        {
            var request = new GetWalletListRequest() {CredentialGuid = credentialGuid};
            var result = await WalletServiceWrapper.GetWalletList(InjectAuthorization(request));
            return new JsonResult(result);
        }

        [HttpPost("Adjust")]
        public async Task<JsonResult> Adjust([FromBody] UpdateWalletRequest request)
        {
            var result = await WalletServiceWrapper.UpdateWallet(InjectAuthorization(request));
            return new JsonResult(result);
        }

        [HttpPost("Delete")]
        public async Task<JsonResult> Delete([FromBody] DeleteWalletRequest request)
        {
            var result = await WalletServiceWrapper.DeleteWallet(InjectAuthorization(request));
            return new JsonResult(result);
        }

        [HttpPost("Increment")]
        public async Task<JsonResult> Increment([FromBody] IncrementWalletRequest request)
        {
            var result = await WalletServiceWrapper.IncrementWallet(InjectAuthorization(request));
            return new JsonResult(result);
        }

        [HttpPost("Decrement")]
        public async Task<JsonResult> Decrement([FromBody] DecrementWalletRequest request)
        {
            var result = await WalletServiceWrapper.DecrementWallet(InjectAuthorization(request));
            return new JsonResult(result);
        }

        [HttpPost("Transfer")]
        public async Task<JsonResult> Transfer([FromBody] TransferWalletRequest request)
        {
            var result = await WalletServiceWrapper.TransferWallet(InjectAuthorization(request));
            return new JsonResult(result);
        }
        
        [HttpPost("Deposit")]
        public async Task<JsonResult> Deposit([FromBody] CreateWalletDepositRequest request)
        {
            var result = await WalletServiceWrapper.CreateDepositRequest(InjectAuthorization(request));
            return new JsonResult(result);
        }
        
        [HttpPost("Withdraw")]
        public async Task<JsonResult> Withdraw([FromBody] CreateWalletWithdrawalRequest request)
        {
            var result = await WalletServiceWrapper.CreateWithdrawalRequest(InjectAuthorization(request));
            return new JsonResult(result);
        }
    }
}