using System;
using System.Collections.Generic;

namespace IdentityServer.Domain.DataTransferObjects.Legacy;

public partial class TblUserMap
{
    public TblUserMap()
    {
        TblUserBinaryListSourceUserMap = new HashSet<TblUserBinaryList>();
        TblUserBinaryListTargetUserMap = new HashSet<TblUserBinaryList>();
        TblUserIncomeTransactionPairMap = new HashSet<TblUserIncomeTransaction>();
        TblUserIncomeTransactionSourceMap = new HashSet<TblUserIncomeTransaction>();
        TblUserIncomeTransactionTargetMap = new HashSet<TblUserIncomeTransaction>();
    }

    public long Id { get; set; }
    public bool? IsEnabled { get; set; }
    public DateTime? CreatedOn { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public long? ModifiedBy { get; set; }
    public DateTime? LastChanged { get; set; }
    public string UserUid { get; set; }
    public long? SponsorUserId { get; set; }
    public long? UplineUserId { get; set; }
    public int Position { get; set; }
    public long LeftLeg { get; set; }
    public long RightLeg { get; set; }
    public string Alias { get; set; }
    public long? Level { get; set; }
    public long? BinaryLeft { get; set; }
    public long? BinaryRight { get; set; }

    public virtual TblUserBusinessPackage IdNavigation { get; set; }
    public virtual TblUserAuth SponsorUser { get; set; }
    public virtual TblUserBusinessPackage UplineUser { get; set; }
    public virtual ICollection<TblUserBinaryList> TblUserBinaryListSourceUserMap { get; set; }
    public virtual ICollection<TblUserBinaryList> TblUserBinaryListTargetUserMap { get; set; }
    public virtual ICollection<TblUserIncomeTransaction> TblUserIncomeTransactionPairMap { get; set; }
    public virtual ICollection<TblUserIncomeTransaction> TblUserIncomeTransactionSourceMap { get; set; }
    public virtual ICollection<TblUserIncomeTransaction> TblUserIncomeTransactionTargetMap { get; set; }
}