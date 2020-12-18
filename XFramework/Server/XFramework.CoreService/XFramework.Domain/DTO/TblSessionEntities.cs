using System;
using System.Collections.Generic;

namespace XFramework.Domain.DTO
{
    public partial class TblSessionEntities
    {
        public TblSessionEntities()
        {
            TblSessionData = new HashSet<TblSessionData>();
        }

        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public string Name { get; set; }

        public virtual ICollection<TblSessionData> TblSessionData { get; set; }
    }
}
