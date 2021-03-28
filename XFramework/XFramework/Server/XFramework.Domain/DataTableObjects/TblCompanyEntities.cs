using System;

namespace XFramework.Domain.DataTableObjects
{
    public partial class TblCompanyEntities
    {
        public long Id { get; set; }
        public bool IsEnabled { get; set; }
        public DateTime CreatedOn { get; set; }
        public long? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? ModifiedBy { get; set; }
        public bool IsDeleted { get; set; }
        public string Name { get; set; }

        public virtual TblCompanies TblCompanies { get; set; }
    }
}
