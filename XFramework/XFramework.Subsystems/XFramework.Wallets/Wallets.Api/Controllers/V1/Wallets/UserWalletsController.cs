using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Wallets.Core.DataAccess.Commands.Entity.Wallets;
using Wallets.Core.DataAccess.Query.Entity.Wallets;

namespace Wallets.Api.Controllers.V1.Wallets
{
    [Microsoft.AspNetCore.Components.Route("API/v1/[controller]")]
    [ApiController]
    public class UserWalletsController : XFrameworkControllerBase
    {
        public UserWalletsController(IMediator mediator)
        {
            _mediator = mediator;
        }
        
        [HttpPost("Create")]
        public async Task<JsonResult> Create([FromBody] CreateWalletCmd request)
        {
            var result = await _mediator.Send(request);
            return new JsonResult(result);
        }

        [HttpPost("Get")]
        public async Task<JsonResult> Get([FromBody] GetWalletQuery request)
        {
            var result = await _mediator.Send(request);
            return new JsonResult(result);
        }

        [HttpPost("GetAll")]
        public async Task<JsonResult> GetAll([FromBody] GetAllWalletQuery request)
        {
            var result = await _mediator.Send(request);
            return new JsonResult(result);
        }

        [HttpPost("Adjust")]
        public async Task<JsonResult> Adjust([FromBody] UpdateWalletCmd request)
        {
            var result = await _mediator.Send(request);
            return new JsonResult(result);
        }

        [HttpPost("Delete")]
        public async Task<JsonResult> Delete([FromBody] DeleteWalletCmd request)
        {
            var result = await _mediator.Send(request);
            return new JsonResult(result);
        }

        [HttpPost("Transfer")]
        public async Task<JsonResult> Transfer([FromBody] TransferWalletCmd request)
        {
            var result = await _mediator.Send(request);
            return new JsonResult(result);
        }
        
        [HttpPost("Increment")]
        public async Task<JsonResult> Increment([FromBody] IncrementWalletCmd request)
        {
            var result = await _mediator.Send(request);
            return new JsonResult(result);
        }
        
        
    }
}