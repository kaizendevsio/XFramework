using Wallets.Domain.Generic.Contracts.Requests.Create;
using Wallets.Domain.Generic.Contracts.Requests.Delete;
using Wallets.Domain.Generic.Contracts.Requests.Get;
using Wallets.Domain.Generic.Contracts.Requests.Update;

namespace XFramework.Api.Controllers.V2.Wallets
{
    [Authorize]
    [Route("Api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [ApiVersion("2.0")]
    public class WalletEntityController : XFrameworkControllerBase
    {
        public IWalletServiceWrapper WalletServiceWrapper { get; }

        public WalletEntityController(IWalletServiceWrapper walletServiceWrapper)
        {
            WalletServiceWrapper = walletServiceWrapper;
        }

        [HttpPost]
        public async Task<JsonResult> Create([FromBody] CreateWalletEntityRequest request)
        {
            var result = await WalletServiceWrapper.CreateWalletEntity(request);
            return new JsonResult(result);
        }

        [EnableQuery]
        [HttpGet]
        public async Task<JsonResult> Get(Guid? guid)
        {
            var request = new GetWalletEntityRequest() {Guid = guid};
            var result = await WalletServiceWrapper.GetWalletEntity(request);
            return new JsonResult(result);
        }

        [EnableQuery]
        [HttpGet("List")]
        public async Task<JsonResult> List(Guid? applicationGuid)
        {
            var request = new GetWalletEntityListRequest(){ApplicationGuid = applicationGuid};
            var result = await WalletServiceWrapper.GetWalletEntityList(request);
            return new JsonResult(result);
        }

        [HttpPatch]
        public async Task<JsonResult> Update([FromBody] UpdateWalletEntityRequest request)
        {
            var result = await WalletServiceWrapper.UpdateWalletEntity(request);
            return new JsonResult(result);
        }

        [HttpDelete]
        public async Task<JsonResult> Delete([FromBody] DeleteWalletEntityRequest request)
        {
            var result = await WalletServiceWrapper.DeleteWalletEntity(request);
            return new JsonResult(result);
        }

     
    }
}