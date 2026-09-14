namespace IdentityServer.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record PutEncryptionDirectoryRequest : RequestBase,
    ICommand<QueryResponse<EncryptionDirectoryResponse>>,
    IBoltRequest<PutEncryptionDirectoryRequest, QueryResponse<EncryptionDirectoryResponse>>
{
    public long ExpectedRevision { get; set; }
    public string RootPublicKey { get; set; } = "";
    public string Roster { get; set; } = "";
    public List<EncryptionDevice> Devices { get; set; } = [];
}
