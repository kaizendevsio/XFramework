using Microsoft.AspNetCore.Mvc;
using Wallets.Core.DataAccess.Commands.Entity.ExchangeRate;

namespace Wallets.Api.Controllers.V1.ExchangeRate;

[Route("API/v1/[controller]")]
[ApiController]

public class ExchangeRateController : XFrameworkControllerBase
{
    public ExchangeRateController(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    [HttpPost("Create")]
    public async Task<JsonResult> Create([FromBody] CreateExchangeRateCmd request)
    {
        var result = await _mediator.Send(request);
        return new JsonResult(result);
    }
    
    [HttpPut("Update")]
    public async Task<JsonResult> Update([FromBody] UpdateExchangeRateCmd request)
    {
        var result = await _mediator.Send(request);
        return new JsonResult(result);
    }
    
    [HttpDelete("Delete")]
    public async Task<JsonResult> Delete([FromBody] DeleteExchangeRateCmd request)
    {
        var result = await _mediator.Send(request);
        return new JsonResult(result);
    }
}