using System;
using System.Threading.Tasks;
using Mapster;
using IdentityServer.Domain.Generic.Contracts.Requests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XFramework.Core.DataAccess.Commands.Entity.Identity.Credential;
using XFramework.Core.DataAccess.Query.Entity.Identity;
using XFramework.Integration.Interfaces.Wrappers;

namespace XFramework.Api.Controllers.V1.Identity
{
    [Route("Api/Identity/[controller]")]
    [ApiController]
    [ApiVersion("1.0")]
    public class CredentialController : XFrameworkControllerBase
    {
        private readonly IIdentityServiceWrapper _identityServiceWrapper;

        public CredentialController(IMediator mediator, IIdentityServiceWrapper identityServiceWrapper)
        {
            _identityServiceWrapper = identityServiceWrapper;
            _mediator = mediator;
        }
        
        [Authorize]
        [HttpPost("List")]
        public async Task<JsonResult> GetList([FromBody] GetIdentityCredentialListRequest request)
        {
            var result = await _identityServiceWrapper.GetIdentityCredentialList(request);
            return new JsonResult(result);
        }
        
        [Authorize]
        [HttpPost("Validate")]
        public async Task<JsonResult> Validate([FromBody] CheckCredentialExistenceRequest request)
        {
            var result = await _identityServiceWrapper.CheckCredentialExistence(request);
            return new JsonResult(result);
        }
        
        [Authorize]
        [HttpPost("Create")]
        public async Task<JsonResult> Create([FromBody] CreateCredentialRequest request)
        {
            var result = await _identityServiceWrapper.CreateCredential(request);
            return new JsonResult(result);
        }
        
        [Authorize]
        [HttpPost("Update")]
        public async Task<JsonResult> Update([FromBody] UpdateCredentialRequest request)
        {
            var result = await _identityServiceWrapper.UpdateCredential(request);
            return new JsonResult(result);
        }

        [Authorize]
        [HttpPost("Delete")]
        public async Task<JsonResult> Delete([FromBody] DeleteCredentialRequest request)
        {
            var result = await _identityServiceWrapper.DeleteCredential(request);
            return new JsonResult(result);
        }
        
        [Authorize]
        [HttpPost("ChangePassword")]
        public async Task<JsonResult> ForgotPassword([FromBody] ChangePasswordRequest request)
        {
            var result = await _identityServiceWrapper.ChangePassword(request);
            return new JsonResult(result);
        }
    }
}