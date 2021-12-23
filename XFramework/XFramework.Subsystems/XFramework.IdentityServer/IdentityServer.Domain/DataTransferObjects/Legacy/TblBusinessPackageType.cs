using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects.Legacy
{
    public partial class TblBusinessPackageType
    {
        public TblBusinessPackageType()
        {
            TblBusinessPackage = new HashSet<TblBusinessPackage>();
        }

        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? LastChanged { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        public virtual ICollection<TblBusinessPackage> TblBusinessPackage { get; set; }
    }
}
