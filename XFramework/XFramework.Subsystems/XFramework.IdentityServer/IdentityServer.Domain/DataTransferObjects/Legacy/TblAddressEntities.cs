using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects.Legacy
{
    public partial class TblAddressEntities
    {
        public TblAddressEntities()
        {
            TblUserAddresses = new HashSet<TblUserAddresses>();
        }

        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public string Name { get; set; }

        public virtual ICollection<TblUserAddresses> TblUserAddresses { get; set; }
    }
}
