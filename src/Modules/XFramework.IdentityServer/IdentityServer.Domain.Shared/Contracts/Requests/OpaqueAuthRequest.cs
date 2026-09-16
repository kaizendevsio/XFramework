namespace IdentityServer.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record OpaqueAuthRequest : RequestBase,
    ICommand<QueryResponse<OpaqueAuthResponse>>, IBoltRequest<OpaqueAuthRequest, QueryResponse<OpaqueAuthResponse>>
{
    public string Stage { get; set; } = "";
    public string UserName { get; set; } = "";
    public Guid RoleId { get; set; }
    public Guid ExchangeId { get; set; }
    public string Message { get; set; } = "";
    public string? Record { get; set; }
    public string? WrappedRecovery { get; set; }
    public string? DisplayName { get; set; }
    public PutEncryptionDirectoryRequest? Directory { get; set; }
    public string? RecoveryArchive { get; set; }
    public override string ToString() => nameof(OpaqueAuthRequest);
    public string? LegacyPassword { get; set; }
}
