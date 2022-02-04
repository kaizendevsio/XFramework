using System;

namespace IdentityServer.Domain.DataTransferObjects.Legacy;

public partial class TblUserIncomeTransaction
{
    public long Id { get; set; }
    public bool? IsEnabled { get; set; }
    public DateTime? CreatedOn { get; set; }
    public long? CreatedBy { get; set; }
    public DateTime? ModifiedOn { get; set; }
    public long? ModifiedBy { get; set; }
    public DateTime? LastChanged { get; set; }
    public long UserAuthId { get; set; }
    public long? IncomeTypeId { get; set; }
    public decimal? IncomePercentage { get; set; }
    public short? TransactionType { get; set; }
    public long? SourceMapId { get; set; }
    public short? IncomeStatus { get; set; }
    public string Remarks { get; set; }
    public long? TargetMapId { get; set; }
    public long? PairMapId { get; set; }

    public virtual TblIncomeType IncomeType { get; set; }
    public virtual TblUserMap PairMap { get; set; }
    public virtual TblUserMap SourceMap { get; set; }
    public virtual TblUserMap TargetMap { get; set; }
    public virtual TblUserAuth UserAuth { get; set; }
}