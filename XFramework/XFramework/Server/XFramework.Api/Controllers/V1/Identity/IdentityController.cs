using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XFramework.Core.DataAccess.Commands.Entity.Identity;
using XFramework.Domain.Generic.Contracts.Requests.Create;


// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace XFramework.Api.Controllers.V1.Identity
{
    [Route("Api/v{version:apiVersion}/[controller]")]
    [Route("Api/[controller]")]
    [ApiController]
    [ApiVersion("1.0")]
    public class IdentityController : XFrameworkControllerBase
    {
        private readonly IIdentityServiceWrapper _identityServiceWrapper;

        public IdentityController(IMediator mediator, IIdentityServiceWrapper identityServiceWrapper)
        {
            _identityServiceWrapper = identityServiceWrapper;
            _mediator = mediator;
        }

        [Authorize]
        [HttpGet]
        public async Task<JsonResult> Get(Guid guid)
        {
            var result = await _identityServiceWrapper.GetIdentity(new () { Guid = guid });
            return new JsonResult(result);
        }
        
        [Authorize]
        [HttpPost("Validate")]
        public async Task<JsonResult> Validate([FromBody] CheckIdentityExistenceRequest request)
        {
            var result = await _identityServiceWrapper.CheckIdentityExistence(request);
            return new JsonResult(result);
        }
        
        [Authorize]
        [HttpPost("Create")]
        public async Task<JsonResult> Create([FromBody] CreateUserRequest request)                                                                                                                                                                              
        {
            var result = await _mediator.Send(request.Adapt<CreateIdentityCmd>()).ConfigureAwait(false);
            return new JsonResult(result);
        }
        
        [Authorize]
        [HttpPost("Update")]
        public async Task<JsonResult> Update([FromBody] UpdateIdentityRequest request)
        {
            var result = await _identityServiceWrapper.UpdateIdentity(request);
            return new JsonResult(result);
        }

        [Authorize]
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
