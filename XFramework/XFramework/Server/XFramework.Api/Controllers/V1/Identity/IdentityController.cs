using System;
using System.Threading.Tasks;
using IdentityServer.Domain.Generic.Contracts.Requests;
using Mapster;
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
        public async Task Get(Guid guid)
        {
            await _mediator.Send(new GetIdentityQuery(){Uid = guid}).ConfigureAwait(false);
        }
        
        [HttpPost("Create")]
        public async Task Create([FromBody] CreateUserRequest request)
        {
            await _mediator.Send(request.Adapt<CreateIdentityCmd>()).ConfigureAwait(false);
        }
        
        [HttpPost("Update")]
        public async Task Update([FromBody] UpdateIdentityRequest request)
        {
            await _mediator.Send(request.Adapt<UpdateIdentityCmd>()).ConfigureAwait(false);
        }

        [HttpPost("Delete")]
        public async Task Delete([FromBody] DeleteIdentityRequest request)
        {
            await _mediator.Send(request.Adapt<DeleteIdentityCmd>()).ConfigureAwait(false);
        }
       
        [HttpPost("Authenticate")]
        public async Task Authenticate([FromBody] AuthenticateCredentialRequest request)
        {
            await _mediator.Send(request.Adapt<AuthenticateIdentityQuery>()).ConfigureAwait(false);
        }
    }
}
