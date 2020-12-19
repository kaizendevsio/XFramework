﻿using System;
using System.Collections.Generic;

#nullable disable

namespace IdentityServer.Domain.DTO
{
    public partial class TblIdentityAddress
    {
        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public long? UserInfoId { get; set; }
        public string UnitNumber { get; set; }
        public string Street { get; set; }
        public string Building { get; set; }
        public string Barangay { get; set; }
        public string City { get; set; }
        public string Subdivision { get; set; }
        public string Region { get; set; }
        public long? AddressEntitiesId { get; set; }
        public bool? DefaultAddress { get; set; }

        public virtual TblIdentityAddressEntity AddressEntities { get; set; }
        public virtual TblIdentityInfo UserInfo { get; set; }
    }
}
