using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Wallets.Core.DataAccess.Commands.Entity.Wallets;
using Wallets.Core.DataAccess.Query.Entity.Wallets;
using XFramework.Domain.Generic.BusinessObjects;

namespace Wallets.Api.Controllers.V1.Wallets
{
    [Route("API/v1/[controller]")]
    [ApiController]
    
    public class WalletsController : XFrameworkControllerBase
    {
        public WalletsController(IMediator mediator)
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

        [HttpPost("Update")]
        public async Task<JsonResult> Update([FromBody] UpdateWalletCmd request)
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
        public async Task<JsonResult> Transfer([FromBody] CreateWalletCmd request)
        {
            var result = await _mediator.Send(request);
            return new JsonResult(result);
        }
    }
}