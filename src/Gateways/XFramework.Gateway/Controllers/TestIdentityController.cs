using Wallets.Integration.Drivers;

namespace XFramework.Gateway.Controllers;

[ApiController]
public class TestIdentityController : ControllerBase
{
    [HttpGet("Test")]
    public async Task<object> Test([FromServices]IWalletsServiceWrapper walletsServiceWrapper)
    {
        var x = await walletsServiceWrapper.WalletType.GetList(pageSize:100, pageNumber: 0);
        return x;
    }
}