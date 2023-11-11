namespace XFramework.Domain.Generic.Contracts;

public partial class BillerCategory : BaseModel
{
    public Guid ProviderTypeId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }


    public virtual ICollection<Biller> Billers { get; } = new List<Biller>();

    public virtual ProviderType ProviderType { get; set; } = null!;
}