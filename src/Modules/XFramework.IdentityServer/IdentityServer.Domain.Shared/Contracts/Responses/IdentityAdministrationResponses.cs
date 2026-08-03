namespace IdentityServer.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record IdentityAdministrationResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public string? LastName { get; set; }
    public string? Suffix { get; set; }
    public string? IdentityName { get; set; }
    public string? IdentityDescription { get; set; }
    public DateOnly? BirthDate { get; set; }
    public Gender? Gender { get; set; }
    public CivilStatus? CivilStatus { get; set; }
    public bool IsVerified { get; set; }
    public bool IsEnabled { get; set; }
    public Guid ConcurrencyStamp { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}

[MemoryPackable]
public partial record AssignedCredentialRoleResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CredentialId { get; set; }
    public Guid RoleTypeId { get; set; }
    public DateTime RoleExpiration { get; set; }
    public bool IsEnabled { get; set; }
    public Guid ConcurrencyStamp { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ModifiedAt { get; set; }
}
