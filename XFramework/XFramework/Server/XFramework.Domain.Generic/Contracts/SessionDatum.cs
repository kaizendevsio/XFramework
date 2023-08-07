namespace XFramework.Domain.Generic.Contracts;

public partial class SessionDatum
{
    public Guid Id { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }

    public Guid? SessionTypeId { get; set; }

    public Guid CredentialId { get; set; }

    public string? SessionData { get; set; }

    
    public virtual SessionType? SessionType { get; set; }

    public virtual IdentityCredential Credential { get; set; } = null!;
}
