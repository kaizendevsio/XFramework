namespace XFramework.Domain.Generic.Contracts;

public partial class EloadGatewayNumber
{
    public Guid Id { get; set; }

    public Guid? TelcoTypeId { get; set; }

    public string? Gateway { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? ModifiedOn { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public bool? IsEnabled { get; set; }

    public bool? IsDeleted { get; set; }

    
    public virtual EloadTelcoType? EloadTelcoType { get; set; }
}
