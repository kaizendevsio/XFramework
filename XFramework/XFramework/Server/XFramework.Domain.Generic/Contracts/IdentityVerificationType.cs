namespace XFramework.Domain.Generic.Contracts;

public partial class IdentityVerificationType : BaseModel
{
    public string? Name { get; set; }

    public long? DefaultExpiry { get; set; }

    public short? Priority { get; set; }


    public virtual ICollection<IdentityVerification> IdentityVerifications { get; set; } = new List<IdentityVerification>();
}