using System;
using System.Threading.Tasks;
using AutoMapper;
using IdentityServer.Core.DataAccess.Commands.Entity.Identity;
using IdentityServer.Core.DataAccess.Query.Entity.Identity;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServer.Api.Controllers.Identity
{
    [Route("Api/[controller]")]
    [ApiController]
    public class IdentityController : XFrameworkControllerBase
    {
        public IdentityController(IMediator mediator, IMapper mapper)
        {
            _mediator = mediator;
            _mapper = mapper;
        }

        [HttpGet("Discovery")]
        public async Task<JsonResult> Get(Guid id)
        {
            var result = await _mediator.Send(new GetIdentityQuery() { Uid = id});
            return new JsonResult(result);
        }

        [HttpPut("Discovery")]
        public async Task<JsonResult> Put([FromBody] CreateIdentityCmd request)
        {
            var result = await _mediator.Send(request).ConfigureAwait(false);
            return new JsonResult(result);
        }
    }
}