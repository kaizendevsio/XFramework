namespace XFramework.Domain.Generic.Contracts;

public partial record IdentityVerification : BaseModel
{
    public Guid CredentialId { get; set; }

    public Guid? VerificationTypeId { get; set; }

    public short? Status { get; set; }

    public DateTimeOffset? StatusUpdatedOn { get; set; }

    public string? Token { get; set; }

    public DateTime? Expiry { get; set; }


    public virtual IdentityCredential Credential { get; set; } = null!;

    public virtual IdentityVerificationType? VerificationType { get; set; }
}