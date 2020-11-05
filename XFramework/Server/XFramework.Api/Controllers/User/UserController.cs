using System;
using System.Threading.Tasks;
using AutoMapper;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using XFramework.Core.DataAccess.Commands.Entity.User;
using XFramework.Core.Interfaces;
using XFramework.Data.BO;


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

        
        [HttpPut]
        public async Task<IActionResult> Put([FromBody] UserBO body)
        {
            var _c = _mapper.Map<CreateUserCmd>(body);
            var result = await _mediator.Send(_c).ConfigureAwait(true);
            return Ok(result);

        }

        [HttpPost("Authenticate")]
        public async Task<IActionResult> Authenticate([FromBody]UserAuthBO userBO) {
            return Ok();
        }

        [HttpGet("Test")]
        public IActionResult Test()
        {
            return Ok("Hello World");
        }

    }
}
