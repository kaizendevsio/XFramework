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
        
        [HttpGet]
        public async Task<JsonResult> Get(Guid guid)
        {
            var result = await _identityServiceWrapper.GetIdentityRole(new () { Guid = guid });
            return new JsonResult(result);
        }
        
        [HttpGet("List")]
        public async Task<JsonResult> GetList()
        {
            var request = new GetIdentityRoleListRequest();
            var result = await _identityServiceWrapper.GetIdentityRoleList(request);
            return new JsonResult(result);
        }
        
    }
}