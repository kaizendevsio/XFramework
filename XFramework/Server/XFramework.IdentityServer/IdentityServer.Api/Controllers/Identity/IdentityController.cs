using System;
using System.Threading.Tasks;
using IdentityServer.Core.DataAccess.Commands.Entity.Identity;
using IdentityServer.Core.DataAccess.Query.Entity.Identity;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServer.Api.Controllers.Identity
{
    [Route("Api/[controller]")]
    [ApiController]
    public class IdentityController : XFrameworkControllerBase
    {
        public IdentityController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet("All")]
        public async Task<JsonResult> GetAll()
        {
            var result = await _mediator.Send(new GetAllIdentityQuery());
            return new JsonResult(result);
        }
        
        [HttpGet]
        public async Task<JsonResult> Get(Guid id)
        {
            var result = await _mediator.Send(new GetIdentityQuery() { Uid = id});
            return new JsonResult(result);
        }

        [HttpPut]
        public async Task<JsonResult> Put([FromBody] CreateIdentityCmd request)
        {
            var result = await _mediator.Send(request).ConfigureAwait(false);
            return new JsonResult(result);
        }

        [HttpPost]
        public async Task<JsonResult> Post([FromBody] UpdateIdentityCmd request)
        {
            var result = await _mediator.Send(request).ConfigureAwait(false);
            return new JsonResult(result);
        }
        
        [HttpDelete]
        public async Task<JsonResult> Delete([FromBody] DeleteIdentityCmd request)
        {
            var result = await _mediator.Send(request).ConfigureAwait(false);
            return new JsonResult(result);
        }
    }
}