using System;
using System.Collections.Generic;

namespace SmsGateway.Domain.DataTransferObjects
{
    public partial class AddressProvince
    {
        public AddressProvince()
        {
            AddressCities = new HashSet<AddressCity>();
            IdentityAddresses = new HashSet<IdentityAddress>();
        }

        public long Id { get; set; }
        public long PsgcCode { get; set; }
        public string? Description { get; set; }
        public long RegCode { get; set; }
        public long Code { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string Guid { get; set; } = null!;

        public virtual AddressRegion RegCodeNavigation { get; set; } = null!;
        public virtual ICollection<AddressCity> AddressCities { get; set; }
        public virtual ICollection<IdentityAddress> IdentityAddresses { get; set; }
    }
}
