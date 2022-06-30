using MediatR;
using XFramework.Core.DataAccess.Commands.Entity.Identity;
using XFramework.Domain.Generic.Contracts.Requests.Create;


// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace XFramework.Api.Controllers.V2.Identity
{
    [Route("Api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [ApiVersion("2.0")]
    public class IdentityController : XFrameworkControllerBase
    {
        private readonly IIdentityServiceWrapper _identityServiceWrapper;

        public IdentityController(IMediator mediator, IIdentityServiceWrapper identityServiceWrapper)
        {
            _identityServiceWrapper = identityServiceWrapper;
            _mediator = mediator;
        }

        [EnableQuery]
        [Authorize]
        [HttpGet]
        public async Task<ActionResult> Get(Guid guid)
        {
            var result = await _identityServiceWrapper.GetIdentity(new () { Guid = guid });
            return Ok(result);
        }
        
        [Authorize]
        [HttpPost("Validate")]
        public async Task<JsonResult> Validate([FromBody] CheckIdentityExistenceRequest request)
        {
            var result = await _identityServiceWrapper.CheckIdentityExistence(request);
            return new JsonResult(result);
        }
        
        [Authorize]
        [HttpPost]
        public async Task<JsonResult> Create([FromBody] CreateUserRequest request)                                                                                                                                                                              
        {
            var result = await _mediator.Send(request.Adapt<CreateIdentityCmd>()).ConfigureAwait(false);
            return new JsonResult(result);
        }
        
        [Authorize]
        [HttpPatch]
        public async Task<JsonResult> Update([FromBody] UpdateIdentityRequest request, Guid guid)
        {
            request.Guid = guid;
            var result = await _identityServiceWrapper.UpdateIdentity(request);
            return new JsonResult(result);
        }

        [Authorize]
        [HttpDelete]
        public async Task<JsonResult> Delete(Guid guid)
        {
            var request = new DeleteIdentityRequest() { Guid = guid};
            var result = await _identityServiceWrapper.DeleteIdentity(request);
            return new JsonResult(result);
        }
       
        [HttpPost("Authenticate")]
        public async Task<JsonResult> Authenticate([FromBody] AuthenticateCredentialRequest request)
        {
            request.GenerateToken = true;
            var result = await _identityServiceWrapper.AuthenticateCredential(request);
            return new JsonResult(result);
        }
    }
}
