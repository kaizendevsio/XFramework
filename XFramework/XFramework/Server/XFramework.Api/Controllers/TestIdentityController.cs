namespace XFramework.Api.Controllers;

[ApiController]
public class TestIdentityController : ControllerBase
{
    [HttpGet("Test")]
    public async Task<object> Test([FromServices]IIdentityServerServiceWrapper identityServerServiceWrapper)
    {
        var x = await identityServerServiceWrapper.IdentityCredential.GetList(pageSize:100, pageNumber: 0);
        return x;
    }
}