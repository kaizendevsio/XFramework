namespace XFramework.Domain.Shared.Contracts;


[MemoryPackable(GenerateType.CircularReference)]
public partial class IdentityVerification : BaseModel
{
    
    [MemoryPackOrder(0)]
    public Guid CredentialId { get; set; }

    [MemoryPackOrder(1)]
    public Guid? VerificationTypeId { get; set; }

    [MemoryPackOrder(2)]
    public short? Status { get; set; }

    [MemoryPackOrder(3)]
    public DateTimeOffset? StatusUpdatedOn { get; set; }

    [MemoryPackOrder(4)]
    public string? Token { get; set; }

    [MemoryPackOrder(5)]
    public DateTime? Expiry { get; set; }


    [MemoryPackOrder(6)]
    public virtual IdentityCredential Credential { get; set; } = null!;

    [MemoryPackOrder(7)]
    public virtual IdentityVerificationType? VerificationType { get; set; }
}
