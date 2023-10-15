namespace XFramework.Domain.Generic.Contracts;

public partial record SessionDatum : BaseModel
{
    public Guid? SessionTypeId { get; set; }

    public Guid CredentialId { get; set; }

    public string? SessionData { get; set; }


    public virtual SessionType? SessionType { get; set; }

    public virtual IdentityCredential Credential { get; set; } = null!;
}