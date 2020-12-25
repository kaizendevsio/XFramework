using System;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using XFramework.Core.DataAccess.Commands.Entity.User;
using XFramework.Core.Interfaces;
using XFramework.Domain.BO;


// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace XFramework.Api.Controllers.User
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : XFrameworkControllerBase
    {
        public UserController(IMediator mediator, IMapper mapper)
        {
            _mapper = mapper;
            _mediator = mediator;
        }

        [HttpPost("Create")]
        public async Task Create([FromBody] CreateUserCmd request)
        {
            await _mediator.Send(request).ConfigureAwait(false);
        }

       
    }
}
