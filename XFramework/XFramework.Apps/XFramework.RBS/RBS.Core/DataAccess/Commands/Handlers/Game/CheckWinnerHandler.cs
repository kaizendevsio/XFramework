using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using RBS.Core.DataAccess.Commands.Entity.Game;
using RBS.Core.Interfaces;
using RBS.Domain.BusinessObjects;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Net;
using RBS.Domain.Enums;

namespace RBS.Core.DataAccess.Commands.Handlers.Game
{
    public class CheckWinnerHandler : CommandBaseHandler, IRequestHandler<CheckWinnerCmd, CmdResponseBO<CheckWinnerCmd>>
    {
        public CheckWinnerHandler(IMediator mediator, IDataLayer dataLayer)
        {
            _mediator = mediator;
            _dataLayer = dataLayer;
        }
        
        public async Task<CmdResponseBO<CheckWinnerCmd>> Handle(CheckWinnerCmd request, CancellationToken cancellationToken)
        {
            var fightHistory = await _dataLayer.FightHistories
                .Include(i => i.FightTransactions)
                .Where(i => i.Winner == null)
                .OrderBy(i => i.Id)
                .LastOrDefaultAsync(cancellationToken);

            
            var timeNow = DateTime.Now.ToUniversalTime();
            var timeFight = fightHistory.CreatedAt.Value.ToUniversalTime();
            if ((timeNow - timeFight).TotalMinutes < 4)
            {
                return new()
                {
                    Message = "The game is not yet finished",
                    HttpStatusCode = HttpStatusCode.NotFound,
                    Request = request
                };
            }
            
            foreach (var fightTransaction in fightHistory.FightTransactions)
            {
                var wallet = await _dataLayer.TblUserWallets
                    .Where(i => i.UserAuthId == fightTransaction.UserAuthId)
                    .Where(i => i.WalletTypeId == 1)
                    .FirstOrDefaultAsync(cancellationToken);

                wallet.Balance += request.BetOption switch
                {
                    BetOption.Draw => fightTransaction.BetDraw * 2,
                    BetOption.Meron => fightTransaction.BetMeron * 2,
                    BetOption.Wala => fightTransaction.BetWala * 2,
                    _ => throw new ArgumentOutOfRangeException()
                };
            }

            fightHistory.Winner = request.BetOption.ToString();

            await _dataLayer.SaveChangesAsync(cancellationToken);
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Request = request
            };
        }
    }
}