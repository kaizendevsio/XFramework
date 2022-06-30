using System;
using System.Collections.Generic;

namespace Messaging.Domain.DataTransferObjects
{
    public partial class IdentityAddressEntity
    {
        public IdentityAddressEntity()
        {
            IdentityAddresses = new HashSet<IdentityAddress>();
        }

        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public string? Name { get; set; }
        public string Guid { get; set; } = null!;

        public virtual ICollection<IdentityAddress> IdentityAddresses { get; set; }
    }
}
