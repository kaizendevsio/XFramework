namespace IdentityServer.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record UploadOwnAvatarRequest : RequestBase,
    ICommand<QueryResponse<CredentialAvatarResponse>>,
    IBoltRequest<UploadOwnAvatarRequest, QueryResponse<CredentialAvatarResponse>>
{
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public byte[]? FileBytes { get; set; }
}
