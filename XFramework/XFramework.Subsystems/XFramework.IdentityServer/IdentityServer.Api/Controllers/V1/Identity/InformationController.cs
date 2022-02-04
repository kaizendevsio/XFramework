using IdentityServer.Core.DataAccess.Query.Entity.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServer.Api.Controllers.V1.Identity;

[Route("Api/V1/Identity/[controller]")]
[ApiController]
[Authorize]
public class InformationController : XFrameworkControllerBase
{
    public InformationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("All")]
    public virtual async Task<JsonResult> GetAll()
    {
        var result = await _mediator.Send(new GetAllIdentityQuery());
        return new JsonResult(result);
    }
        
    [HttpGet]
    public virtual async Task<JsonResult> Get(Guid guid)
    {
        var result = await _mediator.Send(new GetIdentityQuery() { Guid = guid });
        return new JsonResult(result);
    }

    [HttpPost]
    public virtual async Task<JsonResult> Post([FromBody] CreateIdentityCmd request)
    {
        var result = await _mediator.Send(request).ConfigureAwait(false);
        return new JsonResult(result);
    }

    [HttpPut]
    public virtual async Task<JsonResult> Put([FromBody] UpdateIdentityCmd request, Guid guid)
    {
        request.Guid = guid;
        var result = await _mediator.Send(request).ConfigureAwait(false);
        return new JsonResult(result);
    }
        
    [HttpDelete]
    public virtual async Task<JsonResult> Delete(Guid guid)
    {
        var request = new DeleteIdentityCmd()
        {
            Guid = guid
        };
        var result = await _mediator.Send(request).ConfigureAwait(false);
        return new JsonResult(result);
    }
}