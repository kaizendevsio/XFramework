using System.Threading.Tasks;
using AutoMapper;
using IdentityServer.Core.DataAccess.Commands.Entity.Identity;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServer.Api.Controllers.Identity
{
    [Route("Api/Identity/[controller]")]
    [ApiController]
    public class DiscoveryController : XFrameworkControllerBase
    {
        public DiscoveryController(IMediator mediator, IMapper mapper)
        {
            _mediator = mediator;
            _mapper = mapper;
        }

        /*[HttpGet("All")]
        public async Task<JsonResult> Get()
        {
            
        }*/

        [HttpPut]
        public async Task<JsonResult> Put([FromBody] CreateIdentityCmd request)
        {
            var result = await _mediator.Send(request).ConfigureAwait(false);
            return new JsonResult(result);
        }
    }
}