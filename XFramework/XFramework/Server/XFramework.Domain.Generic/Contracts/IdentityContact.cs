namespace XFramework.Domain.Generic.Contracts;

public partial class IdentityContact
{
    public Guid Id { get; set; }

    public bool IsEnabled { get; set; }

    public DateTime CreatedAt { get; set; }

    public long? CreatedBy { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public long? ModifiedBy { get; set; }

    public bool IsDeleted { get; set; }

    public Guid? TypeId { get; set; }

    public string Value { get; set; } = null!;

    public long? UserCredentialId { get; set; }

    
    public long GroupId { get; set; }

    public virtual IdentityContactType? Type { get; set; }

    public virtual IdentityContactGroup Group { get; set; } = null!;

    public virtual IdentityCredential? UserCredential { get; set; }
}
