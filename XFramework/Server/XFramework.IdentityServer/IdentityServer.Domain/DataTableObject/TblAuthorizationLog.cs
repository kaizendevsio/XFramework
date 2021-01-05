using System;
using System.Collections.Generic;

#nullable disable

namespace IdentityServer.Domain.DataTableObject
{
    public partial class TblAuthorizationLog
    {
        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedAt { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? LastChanged { get; set; }
        public long IdentityCredentialsId { get; set; }
        public string Ipaddress { get; set; }
        public bool? IsSuccess { get; set; }
        public short? AuthStatus { get; set; }
        public string LoginSource { get; set; }
        public string DeviceName { get; set; }

        public virtual TblIdentityCredential IdentityCredentials { get; set; }
    }
}
