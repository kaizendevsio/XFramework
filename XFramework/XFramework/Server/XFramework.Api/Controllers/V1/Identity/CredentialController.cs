using System;
using System.Threading.Tasks;
using Mapster;
using IdentityServer.Domain.Generic.Contracts.Requests;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using XFramework.Core.DataAccess.Commands.Entity.Identity.Credential;
using XFramework.Core.DataAccess.Query.Entity.Identity;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Api.Controllers.V1.Identity
{
    [Route("Api/V1/Identity/[controller]")]
    [ApiController]
    public class CredentialController : XFrameworkControllerBase
    {
        private readonly IIdentityServiceWrapper _identityServiceWrapper;

        public CredentialController(IMediator mediator, IIdentityServiceWrapper identityServiceWrapper)
        {
            _identityServiceWrapper = identityServiceWrapper;
            _mediator = mediator;
        }
        
        
        [HttpPost("List")]
        public async Task<JsonResult> GetList([FromBody] GetIdentityCredentialListRequest request)
        {
            var result = await _identityServiceWrapper.GetIdentityCredentialList(request);
            return new JsonResult(result);
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