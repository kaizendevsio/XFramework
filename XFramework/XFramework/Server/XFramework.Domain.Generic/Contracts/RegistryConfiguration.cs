namespace XFramework.Domain.Generic.Contracts;

public partial class RegistryConfiguration
{
    public Guid Id { get; set; }

    public Guid ApplicationId { get; set; }

    public string Key { get; set; } = null!;

    public string? Value { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public bool IsDeleted { get; set; }

    public Guid GroupId { get; set; }

    public string? Unit { get; set; }

    
    public virtual Application Application { get; set; } = null!;

    public virtual RegistryConfigurationGroup? Group { get; set; }
}
