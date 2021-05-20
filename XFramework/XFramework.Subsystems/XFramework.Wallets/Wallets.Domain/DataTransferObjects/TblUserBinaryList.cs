using System;
using System.Collections.Generic;

#nullable disable

namespace Wallets.Domain.DataTransferObjects
{
    public partial class TblUserBinaryList
    {
        public long Id { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public bool? IsDeleted { get; set; }
        public long? TargetUserMapId { get; set; }
        public long? SourceUserMapId { get; set; }
        public int? Placement { get; set; }

        public virtual TblUserMap SourceUserMap { get; set; }
        public virtual TblUserMap TargetUserMap { get; set; }
    }
}
