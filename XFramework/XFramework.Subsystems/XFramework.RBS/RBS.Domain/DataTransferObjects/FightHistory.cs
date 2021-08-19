using System;
using System.Collections.Generic;

#nullable disable

namespace RBS.Domain.DataTransferObjects
{
    public partial class FightHistory
    {
        public FightHistory()
        {
            FightTransactions = new HashSet<FightTransaction>();
        }

        public long Id { get; set; }
        public string Uuid { get; set; }
        public long? CategoryId { get; set; }
        public long BetWala { get; set; }
        public long BetMeron { get; set; }
        public long BetDraw { get; set; }
        public string Winner { get; set; }
        public DateTime? CreatedAt { get; set; }

        public virtual FightCategoryEntity Category { get; set; }
        public virtual ICollection<FightTransaction> FightTransactions { get; set; }
    }
}
