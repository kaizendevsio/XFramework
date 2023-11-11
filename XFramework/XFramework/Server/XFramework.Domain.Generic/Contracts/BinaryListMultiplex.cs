namespace XFramework.Domain.Generic.Contracts;

public partial class BinaryListMultiplex : BaseModel
{
    public Guid? BusinessPackageId { get; set; }

    public long? LeftCount { get; set; }

    public long? RightCount { get; set; }

    public long? Level { get; set; }

    public Guid? BinaryMapId { get; set; }

    public virtual BinaryMap? BinaryMap { get; set; }
}