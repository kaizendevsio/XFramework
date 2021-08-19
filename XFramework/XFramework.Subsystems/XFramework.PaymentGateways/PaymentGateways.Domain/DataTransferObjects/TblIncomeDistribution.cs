using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects
{
    public partial class TblIncomeDistribution
    {
        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? LastChanged { get; set; }
        public long BusinessPackageId { get; set; }
        public long IncomeEntityId { get; set; }
        public decimal Value { get; set; }
        public long DistributionType { get; set; }
        public long? MaxLimit { get; set; }
        public long? MinLimit { get; set; }

        public virtual TblIncomeEntity IncomeEntity { get; set; }
    }
}
