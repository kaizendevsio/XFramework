namespace RBS.Domain.Contracts.Requests
{
    public class SubmitBetRequest
    {
        public long UserAuthId { get; set; }
        public long BetWala { get; set; }
        public long BetMeron { get; set; }
        public long BetDraw { get; set; }
        public int? Result { get; set; }
        public long FightId { get; set; }
    }
}