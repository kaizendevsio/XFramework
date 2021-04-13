using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using XFramework.Core.DataAccess.Commands.Entity.User;


// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace XFramework.Api.Controllers.V1.User
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : XFrameworkControllerBase
    {
        public UserController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("Create")]
        public async Task Create([FromBody] CreateUserCmd request)
        {
            await _mediator.Send(request).ConfigureAwait(false);
        }

       
    }
}
