using MediatR;
using RBS.Core.DataAccess.Query.Entity;
using RBS.Core.DataAccess.Query.Entity.Wallet.User;
using RBS.Domain.BusinessObjects;

namespace RBS.Core.DataAccess.Commands.Entity.Game.User
{
    public class GameUserBetCmd : CommandBaseEntity, IRequest<CmdResponseBO<GameUserBetCmd>>
    {
        public long UserAuthId { get; set; }
        public long BetWala { get; set; }
        public long BetMeron { get; set; }
        public long BetDraw { get; set; }
        public int? Result { get; set; }
        public long FightId { get; set; }
    }
}