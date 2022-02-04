using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects.Legacy;

public partial class TblUserBusinessPackage
{
    public TblUserBusinessPackage()
    {
        TblUserMapUplineUser = new HashSet<TblUserMap>();
    }

    public long Id { get; set; }
    public bool? IsEnabled { get; set; }
    public DateTime? CreatedOn { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public long? ModifiedBy { get; set; }
    public DateTime? LastChanged { get; set; }
    public long? UserAuthId { get; set; }
    public long? BusinessPackageId { get; set; }
    public short? PackageStatus { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public long? UserDepositRequestId { get; set; }
    public DateTime? CancellationDate { get; set; }
    public DateTime? ActivationDate { get; set; }
    public long? RecipientAuthId { get; set; }
    public string CodeString { get; set; }
    public long? ConsumedBy { get; set; }

    public virtual TblBusinessPackage BusinessPackage { get; set; }
    public virtual TblUserAuth ConsumedByNavigation { get; set; }
    public virtual TblUserAuth RecipientAuth { get; set; }
    public virtual TblUserAuth UserAuth { get; set; }
    public virtual TblUserDepositRequest UserDepositRequest { get; set; }
    public virtual TblUserMap TblUserMapIdNavigation { get; set; }
    public virtual ICollection<TblUserMap> TblUserMapUplineUser { get; set; }
}