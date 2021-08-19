using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects
{
    public partial class TblUserIncomePartition
    {
        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? LastChanged { get; set; }
        public long? UserRoleId { get; set; }
        public long? IncomeTypeId { get; set; }
        public decimal? Percentage { get; set; }

        public virtual TblIncomeEntity IncomeType { get; set; }
        public virtual TblIdentityRole UserRole { get; set; }
    }
}
