using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using RBS.Core.DataAccess.Commands.Entity.Game.User;
using RBS.Core.DataAccess.Commands.Entity.Wallet.User;
using RBS.Core.Interfaces;
using RBS.Domain.BusinessObjects;
using RBS.Domain.Enums;

namespace RBS.Core.DataAccess.Commands.Handlers.Game.User
{
    public class GameUserBetHandler : CommandBaseHandler, IRequestHandler<GameUserBetCmd, CmdResponseBO<GameUserBetCmd>>
    {
        public GameUserBetHandler(IMediator mediator, IDataLayer dataLayer)
        {
            _mediator = mediator;
            _dataLayer = dataLayer;
        }
        
        public async Task<CmdResponseBO<GameUserBetCmd>> Handle(GameUserBetCmd request, CancellationToken cancellationToken)
        {
            var fightHistory = await _dataLayer.FightHistories
                .Where(i => i.Id == request.FightId)
                .FirstOrDefaultAsync(cancellationToken);
            
            if (fightHistory == null)
            {
                return new()
                {
                    Message = "The requested game was not found",
                    HttpStatusCode = HttpStatusCode.NotFound,
                    Request = request
                };
            }

            var timeNow = DateTime.Now.ToUniversalTime();
            var timeFight = fightHistory.CreatedAt.Value.ToUniversalTime();
            if ((timeNow - timeFight).TotalMinutes > 2)
            {
                return new()
                {
                    Message = "The game betting time is over",
                    HttpStatusCode = HttpStatusCode.NotAcceptable,
                    Request = request
                };
            }
            
            var totalBet = request.BetDraw + request.BetMeron + request.BetWala;
            if (totalBet < 0)
            {
                return new()
                {
                    Message = "Total bet should be greater than 0",
                    HttpStatusCode = HttpStatusCode.NotAcceptable,
                    Request = request
                };
            }
            
            var wallet = await _dataLayer.TblUserWallets.Where(i => i.WalletTypeId == 1).FirstOrDefaultAsync(cancellationToken);
            if (wallet.Balance < totalBet)
            {
                return new()
                {
                    Message = "Insufficient funds",
                    HttpStatusCode = HttpStatusCode.NotAcceptable,
                    Request = request
                };
            }

            wallet.Balance -= totalBet;
            
            fightHistory.BetDraw += request.BetDraw > 0 ? request.BetDraw : 0;
            fightHistory.BetMeron += request.BetMeron > 0 ? request.BetMeron : 0;
            fightHistory.BetWala += request.BetWala > 0 ? request.BetWala : 0;

            var fightTransaction = await _dataLayer.FightTransactions
                .Where(i => i.UserAuthId == request.UserAuthId)
                .Where(i => i.FightId == request.FightId)
                .FirstOrDefaultAsync(cancellationToken);

            if (fightTransaction == null)
            {
                _dataLayer.FightTransactions.Add(new()
                {
                    UserAuthId = request.UserAuthId,
                    FightId = request.FightId,
                    BetWala = request.BetWala > 0 ? request.BetWala : 0,
                    BetMeron = request.BetMeron > 0 ? request.BetMeron : 0,
                    BetDraw = request.BetDraw > 0 ? request.BetDraw : 0,
                    Result = (int?) BetResult.Pending
                });
            }
            else
            {
                fightTransaction.BetWala = request.BetWala > 0 ? request.BetWala : 0;
                fightTransaction.BetMeron = request.BetMeron > 0 ? request.BetMeron : 0;
                fightTransaction.BetDraw = request.BetDraw > 0 ? request.BetDraw : 0;
            }

            await _dataLayer.SaveChangesAsync(cancellationToken);
            
            return new()
            {
                HttpStatusCode = HttpStatusCode.Accepted,
                Request = request
            };

        }
    }
}