
namespace XFramework.Api.Controllers.V2.Identity
{
    [Authorize]
    [Route("Api/v{version:apiVersion}/Identity/[controller]")]
    [ApiController]
    [ApiVersion("2.0")]
    public class ContactsController : XFrameworkControllerBase
    {
        private readonly IIdentityServiceWrapper _identityServiceWrapper;

        public ContactsController(IIdentityServiceWrapper identityServiceWrapper)
        {
            _identityServiceWrapper = identityServiceWrapper;
        }
        
        [EnableQuery]
        [HttpGet]
        public async Task<ActionResult> Get(Guid guid)
        {
            var result = await _identityServiceWrapper.GetContact(new () { Guid = guid });
            return Ok(result);
        }
        
        [EnableQuery]
        [HttpGet("List")]
        public async Task<ActionResult> GetList(Guid? credentialGuid)
        {
            var request = new GetContactListRequest(){CredentialGuid = credentialGuid};
            var result = await _identityServiceWrapper.GetContactList(InjectAuthorization(request));
            return Ok(result);
        }
        
        [HttpGet("Validate")]
        public async Task<JsonResult> Validate([FromBody] CheckContactExistenceRequest request)
        {
            var result = await _identityServiceWrapper.CheckContactExistence(request);
            return new JsonResult(result);
        }
        
        [HttpPost]
        public async Task<JsonResult> Create([FromBody] CreateContactRequest request)
        {
            var result = await _identityServiceWrapper.CreateContact(request);
            return new JsonResult(result);
        }
        
        [HttpPatch]
        public async Task<JsonResult> Update([FromBody] UpdateContactRequest request, Guid guid)
        {
            request.Guid = guid;
            var result = await _identityServiceWrapper.UpdateContact(request);
            return new JsonResult(result);
        }

        [HttpDelete]
        public async Task<JsonResult> Delete(Guid guid)
        {
            var request = new DeleteContactRequest() { Guid = guid};
            var result = await _identityServiceWrapper.DeleteContact(request);
            return new JsonResult(result);
        }
        
    }
}