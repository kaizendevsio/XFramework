using System;
using System.Collections.Generic;

#nullable disable

namespace RBS.Domain.DataTransferObjects
{
    public partial class TblApplication
    {
        public TblApplication()
        {
            TblConfigurations = new HashSet<TblConfiguration>();
            TblIdentityCredentials = new HashSet<TblIdentityCredential>();
            TblLogs = new HashSet<TblLog>();
        }

        public long Id { get; set; }
        public string AppName { get; set; }
        public string Description { get; set; }
        public string Uid { get; set; }
        public short? Status { get; set; }
        public DateTime? Expiration { get; set; }
        public DateTime? AvailabilityDate { get; set; }
        public long? ParentAppId { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long EnterpriseId { get; set; }
        public decimal Version { get; set; }

        public virtual TblEnterprise Enterprise { get; set; }
        public virtual ICollection<TblConfiguration> TblConfigurations { get; set; }
        public virtual ICollection<TblIdentityCredential> TblIdentityCredentials { get; set; }
        public virtual ICollection<TblLog> TblLogs { get; set; }
    }
}
