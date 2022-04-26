using Community.Integration.Interfaces;
using MediatR;


// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace XFramework.Api.Controllers.V2.Community
{
    [Route("Api/v{version:apiVersion}/[controller]")]
    [ApiController]
    [ApiVersion("2.0")]
    public class CommunityController : XFrameworkControllerBase
    {
        private readonly ICommunityServiceWrapper _communityServiceWrapper;

        public CommunityController(IMediator mediator, ICommunityServiceWrapper communityServiceWrapper)
        {
            _communityServiceWrapper = communityServiceWrapper;
            _mediator = mediator;
        }

        [EnableQuery]
        [Authorize]
        [HttpGet]
        public async Task<ActionResult> Get(Guid? credentialGuid, Guid? communityIdentityGuid)
        {
            var result = await _communityServiceWrapper.GetIdentity(new()
            {
                CredentialGuid = credentialGuid,
                CommunityIdentityGuid = communityIdentityGuid
            });
            return Ok(result);
        }
        
        
        [EnableQuery]
        [Authorize]
        [HttpGet("List")]
        public async Task<ActionResult> List(Guid? communityIdentityEntityGuid, int limit = 0)
        {
            var result = await _communityServiceWrapper.GetIdentityList(new()
            {
                Limit = limit,
                CommunityIdentityEntityGuid = communityIdentityEntityGuid
            });
            return Ok(result);
        }

    }
}
