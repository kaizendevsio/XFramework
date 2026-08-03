namespace IdentityServer.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record CredentialAdministrationResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid IdentityInfoId { get; set; }
    public string? UserName { get; set; }
    public string? UserAlias { get; set; }
    public bool IsEnabled { get; set; }
    public Guid ConcurrencyStamp { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public Guid? AvatarStorageFileId { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime? AvatarUpdatedAt { get; set; }
}

[MemoryPackable]
public partial record VerificationAdministrationResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CredentialId { get; set; }
    public Guid? VerificationTypeId { get; set; }
    public short? Status { get; set; }
    public DateTimeOffset? StatusUpdatedOn { get; set; }
    public DateTime? Expiry { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public int FailedAttempts { get; set; }
    public bool IsEnabled { get; set; }
    public Guid ConcurrencyStamp { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

[MemoryPackable]
public partial record TenantAdministrationResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public short? Status { get; set; }
    public DateTime? Expiration { get; set; }
    public DateTime? AvailabilityDate { get; set; }
    public Guid? ParentTenantId { get; set; }
    public decimal Version { get; set; }
    public bool IsEnabled { get; set; }
    public Guid ConcurrencyStamp { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}
