using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects
{
    public partial class TblConfiguration
    {
        public long Id { get; set; }
        public long ApplicationId { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public bool IsDeleted { get; set; }
        public long? GroupId { get; set; }
        public string Unit { get; set; }

        public virtual TblApplication Application { get; set; }
        public virtual TblConfigurationGroup Group { get; set; }
    }
}
