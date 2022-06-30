using Microsoft.AspNetCore.Mvc;
using Wallets.Core.DataAccess.Commands.Entity.CurrencyEntity;

namespace Wallets.Api.Controllers.V1.CurrencyEntity;

[Route("API/v1/[controller]")]
[ApiController]

public class CurrencyEntityController : XFrameworkControllerBase
{
    public CurrencyEntityController(IMediator mediator)
    {
        _mediator = mediator;
    }
    
    [HttpPost("Create")]
    public async Task<JsonResult> Create([FromBody] CreateCurrencyEntityCmd request)
    {
        var response = await _mediator.Send(request);
        return new JsonResult(response);
    }
    
    [HttpPut("Update")]
    public async Task<JsonResult> Update([FromBody] UpdateCurrencyEntityCmd request)
    {
        var response = await _mediator.Send(request);
        return new JsonResult(response);
    }
    
    [HttpDelete("Delete")]
    public async Task<JsonResult> Delete([FromBody] DeleteCurrencyEntityCmd request)
    {
        var response = await _mediator.Send(request);
        return new JsonResult(response);
    }
    
}