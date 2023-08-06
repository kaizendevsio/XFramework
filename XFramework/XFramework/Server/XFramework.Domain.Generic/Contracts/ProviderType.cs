namespace XFramework.Domain.Generic.Contracts;

public partial class ProviderType
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public DateTime? CreatedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public DateTime? ModifiedAt { get; set; }

    
    public virtual ICollection<BillerCategory> BillerCategories { get; } = new List<BillerCategory>();

    public virtual ICollection<ProviderEndpoint> ProviderEndpoints { get; } = new List<ProviderEndpoint>();
}
