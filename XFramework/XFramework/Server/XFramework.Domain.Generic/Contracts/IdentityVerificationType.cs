namespace XFramework.Domain.Generic.Contracts;

public partial record IdentityVerificationType : BaseModel
{
    public string? Name { get; set; }

    public long? DefaultExpiry { get; set; }

    public short? Priority { get; set; }


    public virtual ICollection<IdentityVerification> IdentityVerifications { get; } = new List<IdentityVerification>();
}