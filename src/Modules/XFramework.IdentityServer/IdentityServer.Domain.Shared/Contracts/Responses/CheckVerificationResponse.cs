namespace IdentityServer.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record CheckVerificationResponse
{
    public bool IsVerified { get; init; }
    public VerificationStatusResponse? LastVerification { get; init; }
};

[MemoryPackable]
public partial record VerificationStatusResponse
{
    public Guid Id { get; init; }
    public Guid CredentialId { get; init; }
    public Guid? VerificationTypeId { get; init; }
    public short? Status { get; init; }
    public DateTimeOffset? StatusUpdatedOn { get; init; }
    public DateTime? Expiry { get; init; }
    public DateTime CreatedAt { get; init; }
}
