using System;
using System.Collections.Generic;

#nullable disable

namespace RBS.Domain.DataTransferObjects
{
    public partial class TblUserIncomeTransaction
    {
        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? LastChanged { get; set; }
        public long UserAuthId { get; set; }
        public long? IncomeTypeId { get; set; }
        public decimal? IncomePercentage { get; set; }
        public short? TransactionType { get; set; }
        public long? SourceMapId { get; set; }
        public short? IncomeStatus { get; set; }
        public string Remarks { get; set; }
        public long? TargetMapId { get; set; }
        public long? PairMapId { get; set; }

        public virtual TblUserMap PairMap { get; set; }
        public virtual TblUserMap SourceMap { get; set; }
        public virtual TblUserMap TargetMap { get; set; }
        public virtual TblIdentityCredential UserAuth { get; set; }
    }
}
