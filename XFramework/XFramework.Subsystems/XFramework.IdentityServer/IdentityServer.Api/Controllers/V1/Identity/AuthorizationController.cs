using IdentityServer.Core.DataAccess.Query.Entity.Identity.Credential;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServer.Api.Controllers.V1.Identity;

[Route("Api/V1/Identity/Authorization")]
[ApiController]
public class AuthorizationController : XFrameworkControllerBase
{
    public AuthorizationController(IMediator mediator)
    {
        _mediator = mediator;
    }
      
    [HttpPost("Authenticate")]
    public virtual async Task<JsonResult> Authenticate([FromBody] AuthenticateCredentialQuery request)
    {
        var result = await _mediator.Send(request);
        return new JsonResult(result);
    }
        
    [HttpPost]
    public virtual async Task<JsonResult> Post([FromBody] CreateCredentialCmd request)
    {
        var result = await _mediator.Send(request);
        return new JsonResult(result);
    }
     
    [HttpPut]
    [Authorize]
    public virtual async Task<JsonResult> Put([FromBody] UpdateCredentialCmd request)
    {
        var result = await _mediator.Send(request);
        return new JsonResult(result);
    }
        
    [HttpDelete]
    [Authorize]
    public virtual async Task<JsonResult> Delete([FromBody] DeleteCredentialCmd request)
    {
        var result = await _mediator.Send(request);
        return new JsonResult(result);
    }

}