namespace RBS.Domain.Contracts.Responses
{
    public class LatestGameContract
    {
        public long Id { get; set; }
        public string Uuid { get; set; }
        public long? CategoryId { get; set; }
        public long? BetWala { get; set; }
        public long? BetMeron { get; set; }
        public long? BetDraw { get; set; }
        public string Winner { get; set; }
    }
}