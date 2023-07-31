using System;
using System.Collections.Generic;

#nullable disable

namespace StreamFlow.Domain.DataTransferObjects;

public partial class TblEnterprise
{
    public TblEnterprise()
    {
        TblApplications = new HashSet<TblApplication>();
    }

    public long Id { get; set; }
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public long? ModifiedBy { get; set; }
    public bool IsDeleted { get; set; }
    public string Name { get; set; }

    public virtual ICollection<TblApplication> TblApplications { get; set; }
}