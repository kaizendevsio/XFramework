using System;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using RBS.Core.DataAccess.Commands.Entity.Game;
using RBS.Core.DataAccess.Commands.Entity.Game.User;
using RBS.Core.DataAccess.Commands.Entity.Wallet.User;
using RBS.Core.DataAccess.Query.Entity.Game;
using RBS.Core.DataAccess.Query.Entity.Game.User;

namespace RBS.Api.Controllers.V1.Game
{
    public class GameController : XFrameworkControllerBase
    {
        public GameController(IMediator mediator)
        {
            _mediator = mediator;
        }
        
        [HttpGet("CheckWinner")]
        public async Task<JsonResult> CheckWinner(CheckWinnerCmd request)
        {
            var result = await _mediator.Send(request);
            return new(result);
        }
        
        [HttpGet("LatestGame")]
        public async Task<JsonResult> LatestGame()
        {
            var request = new LatestGameQuery();
            var result = await _mediator.Send(request);
            return new(result);
        }
        
        [HttpPost("Bet")]
        public async Task<JsonResult> Bet(GameUserBetCmd request)
        {
            var result = await _mediator.Send(request);
            return new(result);
        }
        
        [HttpPost("Transactions")]
        public async Task<JsonResult> Transactions(Guid guid, string userName)
        {
            var request = new GameUserTransactionsQuery()
            {
                UserName = userName
            };
            var result = await _mediator.Send(request);
            return new(result);
        }
        
    }
}