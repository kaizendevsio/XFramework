using System;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using PaymentGateways.Core.DataAccess.Query.Entity.Gateway;
using XFramework.Domain.Generic.BusinessObjects;

namespace PaymentGateways.Api.Controllers.V1
{
    [Route("[controller]")]
    [ApiController]
    public class GatewayController : XFrameworkControllerBase
    {
        public GatewayController(IMediator mediator)
        {
            _mediator = mediator;
        }
        
        [HttpGet]
        public async Task<JsonResult> Startup()
        {
            var result = await _mediator.Send(new GetGatewayListQuery()).ConfigureAwait(false);
            return new JsonResult(result);
        }
    }
}