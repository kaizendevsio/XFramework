using System;
using System.Collections.Generic;

namespace XFramework.Data.DTO
{
    public partial class TblUserAuthHistory
    {
        public long Id { get; set; }
        public bool? IsEnabled { get; set; }
        public DateTime? CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime? LastChanged { get; set; }
        public long UserCredentialsId { get; set; }
        public string Ipaddress { get; set; }
        public bool? IsSuccess { get; set; }
        public short? AuthStatus { get; set; }
        public string LoginSource { get; set; }
        public string DeviceName { get; set; }

        public virtual TblUserCredentials UserCredentials { get; set; }
    }
}
