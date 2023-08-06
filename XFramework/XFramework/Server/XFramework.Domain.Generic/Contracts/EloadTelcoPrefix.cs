namespace XFramework.Domain.Generic.Contracts;

public partial class EloadTelcoPrefix
{
    public Guid Id { get; set; }

    public Guid? TelcoTypeId { get; set; }

    public string? Prefix { get; set; }

    public bool? IsEnabled { get; set; }

    public bool? IsDeleted { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public DateTime? ModifiedOn { get; set; }

    
    public virtual EloadTelcoType? TelcoType { get; set; }
}
