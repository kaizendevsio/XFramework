using System;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Authorization;
using IdentityServer.Core.DataAccess.Query.Entity.Identity;
using IdentityServer.Core.DataAccess.Query.Entity.Identity.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServer.Api.Controllers.Identity
{
    [Route("Api/Identity/Authorization")]
    [ApiController]
    public class AuthorizationController : XFrameworkControllerBase
    {
        public AuthorizationController(IMediator mediator)
        {
            _mediator = mediator;
        }
      
        [HttpPost("Authenticate")]
        public async Task<JsonResult> Authenticate([FromBody] AuthenticateIdentityQuery request)
        {
            var result = await _mediator.Send(request);
            return new JsonResult(result);
        }
        
        [HttpPost]
        public async Task<JsonResult> Post([FromBody] CreateAuthorizeIdentityCmd request)
        {
            var result = await _mediator.Send(request);
            return new JsonResult(result);
        }
     
        [HttpPut]
        public async Task<JsonResult> Put([FromBody] UpdateAuthorizeIdentityCmd request)
        {
            var result = await _mediator.Send(request);
            return new JsonResult(result);
        }
        
        [HttpDelete]
        public async Task<JsonResult> Delete([FromBody] DeleteAuthorizeIdentityCmd request)
        {
            var result = await _mediator.Send(request);
            return new JsonResult(result);
        }

    }
}