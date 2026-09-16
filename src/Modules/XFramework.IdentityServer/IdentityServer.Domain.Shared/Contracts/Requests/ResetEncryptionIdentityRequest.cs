namespace IdentityServer.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record ResetEncryptionIdentityRequest : RequestBase,
    ICommand<QueryResponse<EncryptionDirectoryResponse>>,
    IBoltRequest<ResetEncryptionIdentityRequest, QueryResponse<EncryptionDirectoryResponse>>
{
    public Guid OpaqueProof { get; set; }
    public string? WrappedRecovery { get; set; }
    public string Password { get; set; } = "";
    public PutEncryptionDirectoryRequest Directory { get; set; } = new();
    public string RecoveryArchive { get; set; } = "";
    public override string ToString() => nameof(ResetEncryptionIdentityRequest);
}
