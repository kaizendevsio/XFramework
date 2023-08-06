namespace XFramework.Domain.Generic.Contracts;

public partial class EloadPromo
{
    public Guid Id { get; set; }

    public string? Name { get; set; }

    public string? Description { get; set; }

    public DateTime? CreatedAt { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    
    public virtual ICollection<EloadProductCode> TelcoProductCodes { get; } = new List<EloadProductCode>();
}
