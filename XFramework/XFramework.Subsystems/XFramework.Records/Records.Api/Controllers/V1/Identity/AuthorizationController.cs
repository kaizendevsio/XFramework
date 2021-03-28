using IdentityServer.Core.DataAccess.Commands.Entity.Identity.Authorization;
using IdentityServer.Core.DataAccess.Query.Entity.Identity.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Records.Api.Controllers.V1.Identity
{
    [Route("Api/V1/Identity/Authorization")]
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
        [Authorize]
        public async Task<JsonResult> Put([FromBody] UpdateAuthorizeIdentityCmd request)
        {
            var result = await _mediator.Send(request);
            return new JsonResult(result);
        }

        [HttpDelete]
        [Authorize]
        public async Task<JsonResult> Delete([FromBody] DeleteAuthorizeIdentityCmd request)
        {
            var result = await _mediator.Send(request);
            return new JsonResult(result);
        }

    }
}