using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects
{
    public partial class TblIncomeEntity
    {
        public TblIncomeEntity()
        {
            TblIncomeDistributions = new HashSet<TblIncomeDistribution>();
            TblUserIncomePartitions = new HashSet<TblUserIncomePartition>();
            TblUserIncomeTransactions = new HashSet<TblUserIncomeTransaction>();
        }

        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public string IncomeTypeName { get; set; }
        public string IncomeTypeShortName { get; set; }
        public string IncomeTypeDescription { get; set; }
        public bool? IsReward { get; set; }

        public virtual ICollection<TblIncomeDistribution> TblIncomeDistributions { get; set; }
        public virtual ICollection<TblUserIncomePartition> TblUserIncomePartitions { get; set; }
        public virtual ICollection<TblUserIncomeTransaction> TblUserIncomeTransactions { get; set; }
    }
}
