using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects.Legacy;

public partial class TblUserRole
{
    public TblUserRole()
    {
        TblUserIncomePartition = new HashSet<TblUserIncomePartition>();
    }

    public long Id { get; set; }
    public bool? IsEnabled { get; set; }
    public DateTime? CreatedOn { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public long? ModifiedBy { get; set; }
    public DateTime? LastChanged { get; set; }
    public long UserAuthId { get; set; }
    public string AccessRole { get; set; }
    public int? AccountType { get; set; }

    public virtual TblUserAuth UserAuth { get; set; }
    public virtual ICollection<TblUserIncomePartition> TblUserIncomePartition { get; set; }
}