using System.Threading.Tasks;
using AutoMapper;
using IdentityServer.Core.DataAccess.Query.Entity.Application;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServer.Api.Controllers.Application
{
    [Route("Api/Application/[controller]")]
    [ApiController]
    public class DiscoveryController : XFrameworkControllerBase
    {
        public DiscoveryController(IMediator mediator, IMapper mapper)
        {
            _mapper = mapper;
            _mediator = mediator;
        }

        [HttpGet("All")]
        public async Task<JsonResult> Get()
        {
            var q = new AppsListQuery();
            var result = await _mediator.Send(q).ConfigureAwait(false);
            return new JsonResult(result);
        }
        
    }
}