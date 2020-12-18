using System;
using System.Collections.Generic;

namespace XFramework.Domain.DTO
{
    public partial class TblApplication
    {
        public TblApplication()
        {
            TblUserCredentials = new HashSet<TblUserCredentials>();
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

        public virtual ICollection<TblUserCredentials> TblUserCredentials { get; set; }
    }
}
