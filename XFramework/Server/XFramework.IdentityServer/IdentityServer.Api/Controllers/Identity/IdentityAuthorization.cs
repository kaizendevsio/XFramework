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
    public class IdentityAuthorization : XFrameworkControllerBase
    {
        public IdentityAuthorization(IMediator mediator)
        {
            _mediator = mediator;
        }
      
        [HttpPost]
        public async Task<JsonResult> Post([FromBody] AuthorizeIdentityQuery request)
        {
            var result = await _mediator.Send(request);
            return new JsonResult(result);
        }
        
        [HttpPost("Create")]
        public async Task<JsonResult> PostCreate([FromBody] CreateAuthorizeIdentityCmd request)
        {
            var result = await _mediator.Send(request);
            return new JsonResult(result);
        }

    }
}