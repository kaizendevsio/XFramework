namespace IdentityServer.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record ResetEncryptionIdentityRequest : RequestBase,
    ICommand<QueryResponse<EncryptionDirectoryResponse>>,
    IBoltRequest<ResetEncryptionIdentityRequest, QueryResponse<EncryptionDirectoryResponse>>
{
    public string Password { get; set; } = "";
    public PutEncryptionDirectoryRequest Directory { get; set; } = new();
    public string RecoveryArchive { get; set; } = "";
    public override string ToString() => nameof(ResetEncryptionIdentityRequest);
}
