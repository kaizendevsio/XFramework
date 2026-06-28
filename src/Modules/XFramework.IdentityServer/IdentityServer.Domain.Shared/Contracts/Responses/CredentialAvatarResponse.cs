namespace IdentityServer.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record CredentialAvatarResponse
{
    public Guid CredentialId { get; set; }
    public Guid? StorageFileId { get; set; }
    public string? AvatarUrl { get; set; }
    public string? ContentType { get; set; }
    public string? FileName { get; set; }
    public DateTime? AvatarUpdatedAt { get; set; }
}
