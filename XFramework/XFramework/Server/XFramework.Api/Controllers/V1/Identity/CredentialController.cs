using System.Threading.Tasks;
using Mapster;
using IdentityServer.Domain.Generic.Contracts.Requests;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using XFramework.Core.DataAccess.Commands.Entity.Identity.Credential;

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
        public async Task<JsonResult> Create([FromBody] CreateCredentialRequest request)
        {
            var result = await _mediator.Send(request.Adapt<CreateCredentialCmd>()).ConfigureAwait(false);
            return new JsonResult(result);
        }
        
        [HttpPost("Update")]
        public async Task<JsonResult> Update([FromBody] UpdateCredentialRequest request)
        {
            var result = await _mediator.Send(request.Adapt<UpdateCredentialCmd>()).ConfigureAwait(false);
            return new JsonResult(result);
        }

        [HttpPost("Delete")]
        public async Task<JsonResult> Delete([FromBody] DeleteCredentialRequest request)
        {
            var result = await _mediator.Send(request.Adapt<DeleteCredentialCmd>()).ConfigureAwait(false);
            return new JsonResult(result);
        }
    }
}