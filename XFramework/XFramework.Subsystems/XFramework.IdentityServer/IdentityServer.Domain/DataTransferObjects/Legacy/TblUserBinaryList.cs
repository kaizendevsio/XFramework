using System;

namespace IdentityServer.Domain.DataTransferObjects.Legacy;

public partial class TblUserBinaryList
{
    public long Id { get; set; }
    public DateTime? CreatedOn { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public bool? IsDeleted { get; set; }
    public long? TargetUserMapId { get; set; }
    public long? SourceUserMapId { get; set; }
    public int Placement { get; set; }

    public virtual TblUserMap SourceUserMap { get; set; }
    public virtual TblUserMap TargetUserMap { get; set; }
}