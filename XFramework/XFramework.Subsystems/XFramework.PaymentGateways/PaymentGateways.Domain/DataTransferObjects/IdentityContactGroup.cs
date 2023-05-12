using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects
{
    public partial class IdentityContactGroup
    {
        public IdentityContactGroup()
        {
            IdentityContacts = new HashSet<IdentityContact>();
        }

        public long Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool IsDeleted { get; set; }
        public string Name { get; set; }
        public string Guid { get; set; }

        public virtual ICollection<IdentityContact> IdentityContacts { get; set; }
    }
}
