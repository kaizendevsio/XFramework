namespace IdentityServer.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record AuthenticateIdentityResponse
{
    public AuthenticatedIdentityResponse? Identity { get; set; }
    public AuthenticatedCredentialResponse? Credential { get; set; }
    public string? AccessToken { get; set; }
    public string? TokenType { get; set; }
    public int ExpiresIn { get; set; }
    public string? RefreshToken { get; set; }
    public Guid? SessionId { get; set; }
}

[MemoryPackable]
public partial record AuthenticatedIdentityResponse
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
    public bool IsVerified { get; set; }
    public CivilStatus? CivilStatus { get; set; }

    [MemoryPackIgnore]
    public string FullName => string.Join(
        " ",
        new[] { FirstName, MiddleName, LastName, Suffix }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
}

[MemoryPackable]
public partial record AuthenticatedCredentialResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid IdentityInfoId { get; set; }
    public string? UserName { get; set; }
    public string? UserAlias { get; set; }
    public short? LogInStatus { get; set; }
    public bool IsOnline { get; set; }
    public DateTime LastSeen { get; set; }
    public DateTime? OnlineSince { get; set; }
    public string? StatusMessage { get; set; }
    public string? LastActivityType { get; set; }
    public string? Device { get; set; }
    public string? Location { get; set; }
    public Guid? AvatarStorageFileId { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime? AvatarUpdatedAt { get; set; }
}
