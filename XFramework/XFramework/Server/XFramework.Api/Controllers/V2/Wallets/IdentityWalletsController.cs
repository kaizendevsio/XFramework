using Wallets.Domain.Generic.Contracts.Requests.Wallet.Identity;

namespace XFramework.Api.Controllers.V2.Wallets
{
    [Authorize]
    [Route("Api/[controller]")]
    [ApiController]
    [ApiVersion("2.0")]
    public class IdentityWalletsController : XFrameworkControllerBase
    {
        public IWalletServiceWrapper WalletServiceWrapper { get; }

        public IdentityWalletsController(IWalletServiceWrapper walletServiceWrapper)
        {
            WalletServiceWrapper = walletServiceWrapper;
        }

        [HttpPost("Create")]
        public async Task<JsonResult> Create([FromBody] CreateIdentityWalletRequest request)
        {
            var result = await WalletServiceWrapper.CreateIdentityWallet(request);
            return new JsonResult(result);
        }

        [HttpGet("Get")]
        public async Task<JsonResult> Get(long cuid, long walletTypeId)
        {
            var request = new GetIdentityWalletRequest()
            {
                Cuid = cuid,
                WalletTypeId = walletTypeId
            };
            var result = await WalletServiceWrapper.GetIdentityWallet(request);
            return new JsonResult(result);
        }

        [HttpGet("GetAll")]
        public async Task<JsonResult> GetAll(long cuid)
        {
            var request = new GetAllIdentityWalletRequest()
            {
                Cuid = cuid
            };
            
            var result = await WalletServiceWrapper.GetAllIdentityWallet(request);
            return new JsonResult(result);
        }

        [HttpPost("Adjust")]
        public async Task<JsonResult> Adjust([FromBody] UpdateIdentityWalletRequest request)
        {
            var result = await WalletServiceWrapper.UpdateIdentityWallet(request);
            return new JsonResult(result);
        }

        [HttpPost("Delete")]
        public async Task<JsonResult> Delete([FromBody] DeleteIdentityWalletRequest request)
        {
            var result = await WalletServiceWrapper.DeleteIdentityWallet(request);
            return new JsonResult(result);
        }

        [HttpPost("Increment")]
        public async Task<JsonResult> Increment([FromBody] IncrementIdentityWalletRequest request)
        {
            var result = await WalletServiceWrapper.IncrementIdentityWallet(request);
            return new JsonResult(result);
        }

        [HttpPost("Decrement")]
        public async Task<JsonResult> Decrement([FromBody] DecrementIdentityWalletRequest request)
        {
            var result = await WalletServiceWrapper.DecrementIdentityWallet(request);
            return new JsonResult(result);
        }

        [HttpPost("Transfer")]
        public async Task<JsonResult> Transfer([FromBody] TransferIdentityWalletRequest request)
        {
            var result = await WalletServiceWrapper.TransferIdentityWallet(request);
            return new JsonResult(result);
        }
        
        [HttpPost("Deposit")]
        public async Task<JsonResult> Deposit([FromBody] CreateIdentityWalletDepositRequest request)
        {
            var result = await WalletServiceWrapper.CreateDepositRequest(ValidateIdentityElevation(request));
            return new JsonResult(result);
        }
        
        [HttpPost("Withdraw")]
        public async Task<JsonResult> Withdraw([FromBody] CreateIdentityWalletWithdrawalRequest request)
        {
            var result = await WalletServiceWrapper.CreateWithdrawalRequest(ValidateIdentityElevation(request));
            return new JsonResult(result);
        }
    }
}