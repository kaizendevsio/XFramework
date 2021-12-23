using System;

namespace IdentityServer.Domain.DataTransferObjects.Legacy
{
    public partial class TblUserIncomePartition
    {
        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? LastChanged { get; set; }
        public long? UserRoleId { get; set; }
        public long? IncomeTypeId { get; set; }
        public decimal? Percentage { get; set; }

        public virtual TblIncomeType IncomeType { get; set; }
        public virtual TblUserRole UserRole { get; set; }
    }
}
