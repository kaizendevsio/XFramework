using System.Threading.Tasks;
using AutoMapper;
using IdentityServer.Core.DataAccess.Query.Entity.Application;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServer.Api.Controllers.Application
{
    [Route("Api/[controller]")]
    [ApiController]
    public class ApplicationController : XFrameworkControllerBase
    {
        public ApplicationController(IMediator mediator, IMapper mapper)
        {
            _mapper = mapper;
            _mediator = mediator;
        }

        [HttpGet("Discovery/All")]
        public async Task<JsonResult> Get()
        {
            var q = new GetAppAppListQuery();
            var result = await _mediator.Send(q).ConfigureAwait(false);
            return new JsonResult(result);
        }
        
    }
}