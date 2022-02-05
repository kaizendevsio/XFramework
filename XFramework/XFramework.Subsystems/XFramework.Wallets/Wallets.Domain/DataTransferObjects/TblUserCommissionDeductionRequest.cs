using System;
using System.Collections.Generic;

namespace Wallets.Domain.DataTransferObjects
{
    public partial class TblUserCommissionDeductionRequest
    {
        public long Id { get; set; }
        public bool? IsDeleted { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? UserBusinessPackageId { get; set; }
        public decimal? PrincipalAmount { get; set; }
        public decimal? Balance { get; set; }
        public int? Status { get; set; }
        public decimal? DeductionCharge { get; set; }

        public virtual TblUserBusinessPackage UserBusinessPackage { get; set; }
    }
}
