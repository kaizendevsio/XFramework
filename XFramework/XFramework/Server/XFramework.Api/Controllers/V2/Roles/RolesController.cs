using MediatR;

namespace XFramework.Api.Controllers.V2.Roles
{
    [Authorize]
    [Route("Api/[controller]")]
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
        
        [HttpPost("IdentityRoleList")]
        public async Task<JsonResult> GetIdentityRoleList([FromBody] GetIdentityRoleListRequest request)
        {
            var result = await _identityServiceWrapper.GetIdentityRoleList(request);
            return new JsonResult(result);
        }
        
        [HttpPost("List")]
        public async Task<JsonResult> GetRoleEntityList([FromBody] GetRoleEntityListRequest request)
        {
            var result = await _identityServiceWrapper.GetRoleEntityList(request);
            return new JsonResult(result);
        }
        
    }
}