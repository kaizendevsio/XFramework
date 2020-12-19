﻿using System;
using System.Collections.Generic;

#nullable disable

namespace IdentityServer.Domain.DTO
{
    public partial class TblIdentityVerification
    {
        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public bool? IsDeleted { get; set; }
        public long? IdentityCredId { get; set; }
        public long? VerificationTypeId { get; set; }
        public short? Status { get; set; }
        public DateTimeOffset? StatusUpdatedOn { get; set; }
        public string Token { get; set; }
        public DateTime? Expiry { get; set; }

        public virtual TblIdentityCredential IdentityCred { get; set; }
        public virtual TblVerificationType VerificationType { get; set; }
    }
}
