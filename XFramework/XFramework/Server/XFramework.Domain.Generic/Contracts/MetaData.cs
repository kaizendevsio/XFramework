namespace XFramework.Domain.Generic.Contracts;

public partial class MetaData
{
    public Guid Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool IsDeleted { get; set; }

    public Guid TypeId { get; set; }

    public Guid KeyId { get; set; }

    public string? Value { get; set; }

    public virtual MetaDataType Type { get; set; } = null!;
}
