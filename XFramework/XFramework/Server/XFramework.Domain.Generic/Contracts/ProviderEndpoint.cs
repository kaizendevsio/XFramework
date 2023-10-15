namespace XFramework.Domain.Generic.Contracts;

public partial record ProviderEndpoint : BaseModel
{
    public Guid? ProviderId { get; set; }

    public string? Name { get; set; }

    public string? BaseUrlEndpoint { get; set; }


    public string? UrlEndpoint { get; set; }


    public virtual ICollection<Biller> Billers { get; } = new List<Biller>();

    public virtual ProviderType? Provider { get; set; }
}