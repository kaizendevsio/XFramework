using System;
using System.Collections.Generic;

namespace Wallets.Domain.DataTransferObjects
{
    public partial class TblUserBusinessType
    {
        public long Id { get; set; }
        public long IdentityCredentialId { get; set; }
        public long? UserBusinessPackageId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public bool? IsEnabled { get; set; }
        public bool? IsDeleted { get; set; }

        public virtual TblIdentityCredential IdentityCredential { get; set; }
    }
}
