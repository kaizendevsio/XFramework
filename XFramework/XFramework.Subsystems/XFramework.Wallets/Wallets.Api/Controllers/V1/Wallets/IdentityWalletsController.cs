using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Wallets.Core.DataAccess.Commands.Entity.Wallets;
using Wallets.Core.DataAccess.Commands.Entity.Wallets.Identity;
using Wallets.Core.DataAccess.Query.Entity.Wallets;
using Wallets.Core.DataAccess.Query.Entity.Wallets.Identity;

namespace Wallets.Api.Controllers.V1.Wallets;

[Route("API/v1/[controller]")]
[ApiController]
public class IdentityWalletsController : XFrameworkControllerBase
{
    public IdentityWalletsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("Create")]
    public async Task<JsonResult> Create([FromBody] CreateWalletCmd request)
    {
        var result = await _mediator.Send(request);
        return new JsonResult(result);
    }

    [HttpGet("Get")]
    public async Task<JsonResult> Get(Guid? guid)
    {
        var request = new GetWalletQuery()
        {
            Guid = guid
        };
        var result = await _mediator.Send(request);
        return new JsonResult(result);
    }

    [HttpGet("GetAll")]
    public async Task<JsonResult> GetAll()
    {
        var request = new GetWalletListQuery();
        var result = await _mediator.Send(request);
        return new JsonResult(result);
    }

    [HttpPost("Adjust")]
    public async Task<JsonResult> Adjust([FromBody] UpdateWalletCmd request)
    {
        var result = await _mediator.Send(request);
        return new JsonResult(result);
    }

    [HttpPost("Delete")]
    public async Task<JsonResult> Delete([FromBody] DeleteWalletCmd request)
    {
        var result = await _mediator.Send(request);
        return new JsonResult(result);
    }

    [HttpPost("Increment")]
    public async Task<JsonResult> Increment([FromBody] IncrementWalletCmd request)
    {
        var result = await _mediator.Send(request);
        return new JsonResult(result);
    }

    [HttpPost("Decrement")]
    public async Task<JsonResult> Decrement([FromBody] DecrementWalletCmd request)
    {
        var result = await _mediator.Send(request);
        return new JsonResult(result);
    }

    [HttpPost("Transfer")]
    public async Task<JsonResult> Transfer([FromBody] TransferWalletCmd request)
    {
        var result = await _mediator.Send(request);
        return new JsonResult(result);
    }
}