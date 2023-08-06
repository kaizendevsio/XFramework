namespace XFramework.Domain.Generic.Contracts;

public partial class BillerField
{
    public Guid Id { get; set; }

    public Guid BillerId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string Type { get; set; } = null!;

    public decimal? Width { get; set; }

    public DateTime? CreatedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public DateTime? ModifiedAt { get; set; }

    
    public virtual Biller Biller { get; set; } = null!;
}
