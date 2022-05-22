using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects
{
    public partial class CommissionDeductionRequest
    {
        public long Id { get; set; }
        public bool? IsDeleted { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public long? BusinessPackageId { get; set; }
        public decimal? PrincipalAmount { get; set; }
        public decimal? Balance { get; set; }
        public int? Status { get; set; }
        public decimal? DeductionCharge { get; set; }
        public string Guid { get; set; }
    }
}
