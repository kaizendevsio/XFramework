namespace XFramework.Domain.Generic.Contracts;

public partial class BinaryList
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsDeleted { get; set; }

    public Guid? TargetUserMapId { get; set; }

    public Guid? SourceUserMapId { get; set; }

    public int? Placement { get; set; }

    public int? Level { get; set; }

    
    public virtual BinaryMap? SourceUserMap { get; set; }

    public virtual BinaryMap? TargetUserMap { get; set; }
}
