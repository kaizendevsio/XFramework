using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Query.Entity.Application;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServer.Api.Controllers.V1.Application
{
    [Route("Api/V1/[controller]")]
    [ApiController]
    [Authorize]
    public class ApplicationController : XFrameworkControllerBase
    {
        public ApplicationController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("Discovery/All")]
        public virtual async Task<JsonResult> Get()
        {
            var q = new GetAppAppListQuery();
            var result = await _mediator.Send(q).ConfigureAwait(false);
            return new JsonResult(result);
        }
        
    }
}