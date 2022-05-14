using Microsoft.AspNetCore.Mvc;
using Wallets.Core.DataAccess.Commands.Entity.Wallets;
using Wallets.Core.DataAccess.Query.Entity.Wallets;

namespace Wallets.Api.Controllers.V1.Wallets;

[Route("API/v1/[controller]")]
[ApiController]
public class WalletsController : XFrameworkControllerBase
{
    public WalletsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("Create")]
    public async Task<JsonResult> Create([FromBody] CreateWalletEntityCmd request)
    {
        var result = await _mediator.Send(request);
        return new JsonResult(result);
    }

    [HttpGet("Get")]
    public async Task<JsonResult> Get(Guid? guid)
    {
        var request = new GetWalletEntityQuery()
        {
            Guid = guid
        };
            
        var result = await _mediator.Send(request);
        return new JsonResult(result);
    }

    [HttpGet("GetAll")]
    public async Task<JsonResult> GetAll()
    {
        var request = new GetWalletEntityListQuery();
        var result = await _mediator.Send(request);
        return new JsonResult(result);
    }

    [HttpPost("Update")]
    public async Task<JsonResult> Update([FromBody] UpdateWalletEntityCmd request)
    {
        var result = await _mediator.Send(request);
        return new JsonResult(result);
    }

    [HttpPost("Delete")]
    public async Task<JsonResult> Delete([FromBody] DeleteWalletEntityCmd request)
    {
        var result = await _mediator.Send(request);
        return new JsonResult(result);
    }

    [HttpPost("Transfer")]
    public async Task<JsonResult> Transfer([FromBody] CreateWalletEntityCmd request)
    {
        var result = await _mediator.Send(request);
        return new JsonResult(result);
    }
}