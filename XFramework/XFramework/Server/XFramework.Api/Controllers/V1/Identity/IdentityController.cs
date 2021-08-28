using System;
using System.Threading.Tasks;
using Mapster;
using IdentityServer.Domain.Generic.Contracts.Requests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XFramework.Core.DataAccess.Commands.Entity.Identity;
using XFramework.Core.DataAccess.Query.Entity.Identity;
using XFramework.Domain.Generic.Contracts.Requests;
using XFramework.Integration.Interfaces.Wrappers;


// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace XFramework.Api.Controllers.V1.Identity
{
    [Route("Api/V1/Identity")]
    [ApiController]
    public class IdentityController : XFrameworkControllerBase
    {
        private readonly IIdentityServiceWrapper _identityServiceWrapper;

        public IdentityController(IMediator mediator, IIdentityServiceWrapper identityServiceWrapper)
        {
            _identityServiceWrapper = identityServiceWrapper;
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<JsonResult> Get(Guid guid)
        {
            var result = await _identityServiceWrapper.GetIdentity(new GetIdentityRequest() { Uid = guid });
            return new JsonResult(result);
        }
        
        [HttpPost("Validate")]
        public async Task<JsonResult> Validate([FromBody] CheckIdentityExistenceRequest request)
        {
            var result = await _identityServiceWrapper.CheckIdentityExistence(request);
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
            var result = await _identityServiceWrapper.UpdateIdentity(request);
            return new JsonResult(result);
        }

        [HttpPost("Delete")]
        public async Task<JsonResult> Delete([FromBody] DeleteIdentityRequest request)
        {
            var result = await _identityServiceWrapper.DeleteIdentity(request);
            return new JsonResult(result);
        }
       
        [HttpPost("Authenticate")]
        public async Task<JsonResult> Authenticate([FromBody] AuthenticateCredentialRequest request)
        {
            var result = await _identityServiceWrapper.AuthenticateCredential(request);
            return new JsonResult(result);
        }
    }
}
