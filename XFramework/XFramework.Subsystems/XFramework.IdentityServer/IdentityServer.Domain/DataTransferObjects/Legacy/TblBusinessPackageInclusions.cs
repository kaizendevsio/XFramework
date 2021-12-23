using System;

namespace IdentityServer.Domain.DataTransferObjects.Legacy
{
    public partial class TblBusinessPackageInclusions
    {
        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? LastChanged { get; set; }
        public long? BusinessPackageId { get; set; }
        public long? InclusionTypeId { get; set; }
        public decimal? Value { get; set; }
        public string StringValue { get; set; }

        public virtual TblBusinessPackage BusinessPackage { get; set; }
        public virtual TblBusinessPackageInclusionsType InclusionType { get; set; }
    }
}
