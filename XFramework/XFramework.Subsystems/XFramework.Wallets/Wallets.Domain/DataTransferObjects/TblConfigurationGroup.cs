using System;
using System.Collections.Generic;

namespace Wallets.Domain.DataTransferObjects
{
    public partial class TblConfigurationGroup
    {
        public TblConfigurationGroup()
        {
            TblConfigurations = new HashSet<TblConfiguration>();
        }

        public long Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
        public bool? IsDeleted { get; set; }

        public virtual ICollection<TblConfiguration> TblConfigurations { get; set; }
    }
}
