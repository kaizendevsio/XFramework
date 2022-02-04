using System;

namespace IdentityServer.Domain.DataTransferObjects.Legacy;

public partial class TblUserWalletAddress
{
    public long Id { get; set; }
    public bool? IsEnabled { get; set; }
    public DateTime? CreatedOn { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public long? ModifiedBy { get; set; }
    public DateTime? LastChanged { get; set; }
    public long UserAuthId { get; set; }
    public string Address { get; set; }
    public decimal? Balance { get; set; }
    public long WalletTypeId { get; set; }
    public string Remarks { get; set; }

    public virtual TblUserAuth UserAuth { get; set; }
    public virtual TblWalletType WalletType { get; set; }
}