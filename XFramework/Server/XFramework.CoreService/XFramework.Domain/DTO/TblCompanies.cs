using System;
using System.Collections.Generic;

namespace XFramework.Domain.DTO
{
    public partial class TblCompanies
    {
        public TblCompanies()
        {
            TblAddresses = new HashSet<TblAddresses>();
        }

        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public long? CompanyEntitiesId { get; set; }

        public virtual TblCompanyEntities IdNavigation { get; set; }
        public virtual ICollection<TblAddresses> TblAddresses { get; set; }
    }
}
