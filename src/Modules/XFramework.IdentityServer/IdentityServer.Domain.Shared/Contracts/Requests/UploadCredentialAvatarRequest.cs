namespace IdentityServer.Domain.Shared.Contracts.Requests;

using TRequest = UploadCredentialAvatarRequest;
using TResponse = QueryResponse<CredentialAvatarResponse>;

[MemoryPackable]
public partial record UploadCredentialAvatarRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid CredentialId { get; set; }
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public byte[]? FileBytes { get; set; }
}
