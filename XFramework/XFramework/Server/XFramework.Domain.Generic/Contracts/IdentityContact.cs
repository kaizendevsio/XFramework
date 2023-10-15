namespace XFramework.Domain.Generic.Contracts;

public partial record IdentityContact : BaseModel
{
    public Guid? TypeId { get; set; }

    public string Value { get; set; } = null!;

    public Guid CredentialId { get; set; }


    public Guid GroupId { get; set; }

    public virtual IdentityContactType? Type { get; set; }

    public virtual IdentityContactGroup Group { get; set; } = null!;

    public virtual IdentityCredential Credential { get; set; } = null!;
}