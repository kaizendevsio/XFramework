namespace IdentityServer.Domain.Shared.Contracts.Requests;

using TRequest = SetCredentialAvatarRequest;
using TResponse = QueryResponse<CredentialAvatarResponse>;

[MemoryPackable]
public partial record SetCredentialAvatarRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid CredentialId { get; set; }
    public Guid StorageFileId { get; set; }
}
