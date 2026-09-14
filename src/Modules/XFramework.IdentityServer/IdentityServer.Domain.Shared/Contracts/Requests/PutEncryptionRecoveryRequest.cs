namespace IdentityServer.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record PutEncryptionRecoveryRequest : RequestBase,
    ICommand<QueryResponse<EncryptionRecoveryResponse>>,
    IBoltRequest<PutEncryptionRecoveryRequest, QueryResponse<EncryptionRecoveryResponse>>
{
    public long ExpectedRevision { get; set; }
    public string Archive { get; set; } = "";
}
