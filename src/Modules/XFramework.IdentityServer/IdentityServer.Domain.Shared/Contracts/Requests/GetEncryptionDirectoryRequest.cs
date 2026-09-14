namespace IdentityServer.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record GetEncryptionDirectoryRequest : RequestBase,
    ICommand<QueryResponse<EncryptionDirectoryResponse>>,
    IBoltRequest<GetEncryptionDirectoryRequest, QueryResponse<EncryptionDirectoryResponse>>
{
    public Guid CredentialId { get; set; }
    public Guid? SenderDeviceId { get; set; }
}
