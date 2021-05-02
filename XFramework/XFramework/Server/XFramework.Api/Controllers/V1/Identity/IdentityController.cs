using System;
using System.Threading.Tasks;
using Mapster;
using IdentityServer.Domain.Generic.Contracts.Requests;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using XFramework.Core.DataAccess.Commands.Entity.Identity;
using XFramework.Core.DataAccess.Query.Entity.Identity;
using XFramework.Domain.Generic.Contracts.Requests;


// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace XFramework.Api.Controllers.V1.Identity
{
    [Route("Api/V1/Identity")]
    [ApiController]
    public class IdentityController : XFrameworkControllerBase
    {
        public IdentityController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<JsonResult> Get(Guid guid)
        {
            var result = await _mediator.Send(new GetIdentityQuery(){Uid = guid}).ConfigureAwait(false);
            return new JsonResult(result);
        }
        
        [HttpPost("Create")]
        public async Task<JsonResult> Create([FromBody] CreateUserRequest request)
        {
            var result = await _mediator.Send(request.Adapt<CreateIdentityCmd>()).ConfigureAwait(false);
            return new JsonResult(result);
        }
        
        [HttpPost("Update")]
        public async Task<JsonResult> Update([FromBody] UpdateIdentityRequest request)
        {
            var result = await _mediator.Send(request.Adapt<UpdateIdentityCmd>()).ConfigureAwait(false);
            return new JsonResult(result);
        }

        [HttpPost("Delete")]
        public async Task<JsonResult> Delete([FromBody] DeleteIdentityRequest request)
        {
            var result = await _mediator.Send(request.Adapt<DeleteIdentityCmd>()).ConfigureAwait(false);
            return new JsonResult(result);
        }
       
        [HttpPost("Authenticate")]
        public async Task<JsonResult> Authenticate([FromBody] AuthenticateCredentialRequest request)
        {
            var result = await _mediator.Send(request.Adapt<AuthenticateIdentityQuery>()).ConfigureAwait(false);
            return new JsonResult(result);
        }
    }
}
