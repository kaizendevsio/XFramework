using System.Threading.Tasks;
using IdentityServer.Domain.Generic.Contracts.Requests;
using Mapster;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using XFramework.Core.DataAccess.Commands.Entity.Identity.Credential;
using XFramework.Domain.Generic.Contracts.Requests;

namespace XFramework.Api.Controllers.V1.Identity
{
    [Route("Api/V1/Identity/[controller]")]
    [ApiController]
    public class CredentialController : XFrameworkControllerBase
    {
        public CredentialController(IMediator mediator)
        {
            _mediator = mediator;
        }
        
        [HttpPost("Create")]
        public async Task Create([FromBody] CreateCredentialRequest request)
        {
            await _mediator.Send(request.Adapt<CreateCredentialCmd>()).ConfigureAwait(false);
        }
        
        [HttpPost("Update")]
        public async Task Update([FromBody] UpdateCredentialRequest request)
        {
            await _mediator.Send(request.Adapt<UpdateCredentialCmd>()).ConfigureAwait(false);
        }

        [HttpPost("Delete")]
        public async Task Delete([FromBody] DeleteCredentialRequest request)
        {
            await _mediator.Send(request.Adapt<DeleteCredentialCmd>()).ConfigureAwait(false);
        }
    }
}