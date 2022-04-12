using MediatR;

namespace XFramework.Api.Controllers.V2.Identity
{
    [Authorize]
    [Route("Api/v{version:apiVersion}/Identity/[controller]")]
    [ApiController]
    [ApiVersion("2.0")]
    public class RolesController : XFrameworkControllerBase
    {
        private readonly IIdentityServiceWrapper _identityServiceWrapper;

        public RolesController(IMediator mediator, IIdentityServiceWrapper identityServiceWrapper)
        {
            _mediator = mediator;
            _identityServiceWrapper = identityServiceWrapper;
        }
        
        [EnableQuery]
        [HttpGet]
        public async Task<ActionResult> Get(Guid guid)
        {
            var result = await _identityServiceWrapper.GetRole(new () { Guid = guid });
            return Ok(result);
        }
        
        [EnableQuery]
        [HttpGet("List")]
        public async Task<ActionResult> GetList(Guid? credentialGuid)
        {
            var request = new GetRoleListRequest(){CredentialGuid = credentialGuid};
            var result = await _identityServiceWrapper.GetRoleList(InjectAuthorization(request));
            return Ok(result);
        }
        
    }
}