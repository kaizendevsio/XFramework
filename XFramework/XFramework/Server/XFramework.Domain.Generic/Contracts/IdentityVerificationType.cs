namespace XFramework.Domain.Generic.Contracts;

public partial class IdentityVerificationType
{
    public Guid Id { get; set; }

    public bool? IsEnabled { get; set; }

    public DateTime? CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public string? Name { get; set; }

    public long? DefaultExpiry { get; set; }

    public short? Priority { get; set; }

    public bool? IsDeleted { get; set; }

    
    public virtual ICollection<IdentityVerification> IdentityVerifications { get; } = new List<IdentityVerification>();
}
