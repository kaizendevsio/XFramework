namespace XFramework.Domain.Generic.Contracts;

public partial class ProviderEndpoint
{
    public Guid Id { get; set; }

    public Guid? ProviderId { get; set; }

    public string? Name { get; set; }

    public string? BaseUrlEndpoint { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public bool? IsDeleted { get; set; }

    public string? UrlEndpoint { get; set; }

    
    public virtual ICollection<Biller> Billers { get; } = new List<Biller>();

    public virtual ProviderType? Provider { get; set; }
}
