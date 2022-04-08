using MediatR;

namespace XFramework.Api.Controllers.V2.Identity
{
    [Authorize]
    [Route("Api/v{version:apiVersion}/Identity/[controller]")]
    [ApiController]
    [ApiVersion("2.0")]
    public class CredentialController : XFrameworkControllerBase
    {
        private readonly IIdentityServiceWrapper _identityServiceWrapper;

        public CredentialController(IMediator mediator, IIdentityServiceWrapper identityServiceWrapper)
        {
            _identityServiceWrapper = identityServiceWrapper;
            _mediator = mediator;
        }
        
        [EnableQuery]
        [HttpGet("GetByContact")]
        public async Task<JsonResult> Get(string contactValue)
        {
            var result = await _identityServiceWrapper.GetCredentialByContact(new () { ContactValue = contactValue });
            return new JsonResult(result);
        }
        
        [EnableQuery]
        [HttpGet]
        public async Task<JsonResult> Get(Guid guid)
        {
            var result = await _identityServiceWrapper.GetCredential(new () { Guid = guid });
            return new JsonResult(result);
        }
        
        [EnableQuery]
        [HttpGet("List")]
        public async Task<JsonResult> List(Guid? applicationGuid)
        {
            var request = new GetCredentialListRequest(){ApplicationGuid = applicationGuid};
            var result = await _identityServiceWrapper.GetCredentialList(request);
            return new JsonResult(result);
        }
        
        [HttpGet("Validate")]
        public async Task<JsonResult> Validate([FromBody] CheckCredentialExistenceRequest request)
        {
            var result = await _identityServiceWrapper.CheckCredentialExistence(request);
            return new JsonResult(result);
        }
        
        [HttpPost]
        public async Task<JsonResult> Create([FromBody] CreateCredentialRequest request)
        {
            var result = await _identityServiceWrapper.CreateCredential(request);
            return new JsonResult(result);
        }
        
        [HttpPatch]
        public async Task<JsonResult> Update([FromBody] UpdateCredentialRequest request, Guid guid)
        {
            request.Guid = guid;
            var result = await _identityServiceWrapper.UpdateCredential(request);
            return new JsonResult(result);
        }

        [HttpDelete]
        public async Task<JsonResult> Delete(Guid guid)
        {
            var request = new DeleteCredentialRequest() { Guid = guid};
            var result = await _identityServiceWrapper.DeleteCredential(request);
            return new JsonResult(result);
        }
        
        [HttpPatch("ChangePassword")]
        public async Task<JsonResult> ForgotPassword([FromBody] UpdatePasswordRequest request)
        {
            var result = await _identityServiceWrapper.ChangePassword(request);
            return new JsonResult(result);
        }
    }
}