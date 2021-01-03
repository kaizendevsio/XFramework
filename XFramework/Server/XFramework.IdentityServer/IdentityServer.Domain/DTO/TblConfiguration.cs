using System;
using System.Collections.Generic;

#nullable disable

namespace IdentityServer.Domain.DTO
{
    public partial class TblConfiguration
    {
        public long Id { get; set; }
        public long ApplicationId { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public bool IsDeleted { get; set; }

        public virtual TblApplication Application { get; set; }
    }
}
