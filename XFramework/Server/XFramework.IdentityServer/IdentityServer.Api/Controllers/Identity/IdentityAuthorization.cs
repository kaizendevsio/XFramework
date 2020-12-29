using System;
using System.Threading.Tasks;
using AutoMapper;
using IdentityServer.Core.DataAccess.Query.Entity.Identity;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace IdentityServer.Api.Controllers.Identity
{
    [Route("Api/Identity/Authorization")]
    [ApiController]
    public class IdentityAuthorization : XFrameworkControllerBase
    {
        public IdentityAuthorization(IMediator mediator, IMapper mapper)
        {
            _mediator = mediator;
            _mapper = mapper;
        }
        
        [HttpPost]
        public async Task<JsonResult> Post(Guid id)
        {
            var result = await _mediator.Send(new GetIdentityQuery() { Uid = id});
            return new JsonResult(result);
        }

    }
}