namespace XFramework.Domain.Generic.Contracts;

public partial class EloadProductCode
{
    public Guid Id { get; set; }

    public string? Code { get; set; }

    public string? Name { get; set; }

    public string? Validity { get; set; }

    public string? Description { get; set; }

    public bool? IsEnabled { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public bool? IsDeleted { get; set; }

    public Guid? TelcoTypeId { get; set; }

    public Guid? EloadPromoId { get; set; }

    public decimal Amount { get; set; }

    
    public virtual ICollection<EloadTransaction> EloadTransactions { get; } = new List<EloadTransaction>();

    public virtual EloadPromo? EloadPromo { get; set; }

    public virtual EloadTelcoType? EloadTelcoType { get; set; }
}
