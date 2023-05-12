﻿using System;
using System.Collections.Generic;

#nullable disable

namespace PaymentGateways.Domain.DataTransferObjects
{
    public partial class AddressCity
    {
        public AddressCity()
        {
            AddressBarangays = new HashSet<AddressBarangay>();
            IdentityAddresses = new HashSet<IdentityAddress>();
        }

        public long Id { get; set; }
        public long PsgcCode { get; set; }
        public string Description { get; set; }
        public long ProvCode { get; set; }
        public long Code { get; set; }
        public int? RegCode { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string Guid { get; set; }

        public virtual AddressProvince ProvCodeNavigation { get; set; }
        public virtual ICollection<AddressBarangay> AddressBarangays { get; set; }
        public virtual ICollection<IdentityAddress> IdentityAddresses { get; set; }
    }
}
