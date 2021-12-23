using System;

namespace RBS.Domain.Contracts.Responses
{
    public class GameTransactionsContract
    {
        public long? BetWala { get; set; }
        public long? BetMeron { get; set; }
        public long? BetDraw { get; set; }
        public int? Result { get; set; }
        public long? FightId { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}