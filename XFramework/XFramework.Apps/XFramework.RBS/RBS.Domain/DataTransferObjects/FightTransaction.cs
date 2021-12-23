using System;
using System.Collections.Generic;

#nullable disable

namespace RBS.Domain.DataTransferObjects
{
    public partial class FightTransaction
    {
        public long Id { get; set; }
        public long? UserAuthId { get; set; }
        public long BetWala { get; set; }
        public long BetMeron { get; set; }
        public long BetDraw { get; set; }
        public int? Result { get; set; }
        public long? FightId { get; set; }
        public DateTime? CreatedAt { get; set; }

        public virtual FightHistory Fight { get; set; }
        public virtual TblIdentityCredential UserAuth { get; set; }
    }
}
