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
    public class WalletsController : XFrameworkControllerBase
    {
        public IWalletServiceWrapper WalletServiceWrapper { get; }

        public WalletsController(IWalletServiceWrapper walletServiceWrapper)
        {
            WalletServiceWrapper = walletServiceWrapper;
        }

        [HttpPost("Create")]
        public async Task<JsonResult> Create([FromBody] CreateWalletEntityRequest request)
        {
            var result = await WalletServiceWrapper.CreateWalletEntity(request);
            return new JsonResult(result);
        }

        [HttpGet("Get")]
        public async Task<JsonResult> Get(Guid? guid)
        {
            var request = new GetWalletEntityRequest() {Guid = guid};
            var result = await WalletServiceWrapper.GetWalletEntity(request);
            return new JsonResult(result);
        }

        [HttpGet("GetAll")]
        public async Task<JsonResult> GetAll(Guid? applicationGuid)
        {
            var request = new GetWalletEntityListRequest(){ApplicationGuid = applicationGuid};
            var result = await WalletServiceWrapper.GetWalletEntityList(request);
            return new JsonResult(result);
        }

        [HttpPost("Update")]
        public async Task<JsonResult> Update([FromBody] UpdateWalletEntityRequest request)
        {
            var result = await WalletServiceWrapper.UpdateWalletEntity(request);
            return new JsonResult(result);
        }

        [HttpPost("Delete")]
        public async Task<JsonResult> Delete([FromBody] DeleteWalletEntityRequest request)
        {
            var result = await WalletServiceWrapper.DeleteWalletEntity(request);
            return new JsonResult(result);
        }

     
    }
}