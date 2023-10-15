namespace XFramework.Domain.Generic.Contracts;

public partial record BinaryList : BaseModel
{
    public Guid? TargetUserMapId { get; set; }

    public Guid? SourceUserMapId { get; set; }

    public int? Placement { get; set; }

    public int? Level { get; set; }


    public virtual BinaryMap? SourceUserMap { get; set; }

    public virtual BinaryMap? TargetUserMap { get; set; }
}