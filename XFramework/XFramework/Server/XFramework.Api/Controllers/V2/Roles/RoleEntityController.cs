using MediatR;

namespace XFramework.Api.Controllers.V2.Roles
{
    [Authorize]
    [Route("Api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [ApiVersion("2.0")]
    public class RoleEntityController : XFrameworkControllerBase
    {
        private readonly IIdentityServiceWrapper _identityServiceWrapper;

        public RoleEntityController(IMediator mediator, IIdentityServiceWrapper identityServiceWrapper)
        {
            _mediator = mediator;
            _identityServiceWrapper = identityServiceWrapper;
        }
        
        [HttpGet]
        public async Task<JsonResult> Get(Guid guid)
        {
            var result = await _identityServiceWrapper.GetRoleEntity(new () { Guid = guid });
            return new JsonResult(result);
        }
        
        [HttpPost("List")]
        public async Task<JsonResult> GeList(Guid? applicationGuid)
        {
            var request = new GetRoleEntityListRequest(){ApplicationGuid = applicationGuid};
            var result = await _identityServiceWrapper.GetRoleEntityList(request);
            return new JsonResult(result);
        }
        
    }
}