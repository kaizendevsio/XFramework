using System;
using System.Collections.Generic;

#nullable disable

namespace IdentityServer.Domain.DTO
{
    public partial class TblApplication
    {
        public TblApplication()
        {
            TblApplicationLogs = new HashSet<TblApplicationLog>();
            TblIdentityCredentials = new HashSet<TblIdentityCredential>();
        }

        public long Id { get; set; }
        public string AppName { get; set; }
        public string Description { get; set; }
        public string Uid { get; set; }
        public short? Status { get; set; }
        public DateTime? Expiration { get; set; }
        public DateTime? AvailabilityDate { get; set; }
        public long? ParentAppId { get; set; }
        public DateTime? CreatedOn { get; set; }

        public virtual ICollection<TblApplicationLog> TblApplicationLogs { get; set; }
        public virtual ICollection<TblIdentityCredential> TblIdentityCredentials { get; set; }
    }
}
