namespace IdentityServer.Domain.Shared.Contracts.Requests;

using TRequest = RemoveCredentialAvatarRequest;
using TResponse = QueryResponse<CredentialAvatarResponse>;

[MemoryPackable]
public partial record RemoveCredentialAvatarRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid CredentialId { get; set; }
}
