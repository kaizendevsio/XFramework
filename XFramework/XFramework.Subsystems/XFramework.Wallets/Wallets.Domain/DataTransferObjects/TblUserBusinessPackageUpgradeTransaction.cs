using System;
using System.Collections.Generic;

namespace Wallets.Domain.DataTransferObjects
{
    public partial class TblUserBusinessPackageUpgradeTransaction
    {
        public long Id { get; set; }
        public bool IsDeleted { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long CredentialId { get; set; }
        public long UserBusinessPackageId { get; set; }
        public string Uuid { get; set; }
        public long? CurrentBusinessPackageId { get; set; }
        public long PreviousBusinessPackageId { get; set; }
        public int? Status { get; set; }

        public virtual TblIdentityCredential Credential { get; set; }
        public virtual TblUserBusinessPackage UserBusinessPackage { get; set; }
    }
}
