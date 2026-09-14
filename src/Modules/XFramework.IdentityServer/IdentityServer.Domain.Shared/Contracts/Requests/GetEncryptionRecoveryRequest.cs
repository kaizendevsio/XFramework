namespace IdentityServer.Domain.Shared.Contracts.Requests;

[MemoryPackable]
public partial record GetEncryptionRecoveryRequest : RequestBase,
    ICommand<QueryResponse<EncryptionRecoveryResponse>>,
    IBoltRequest<GetEncryptionRecoveryRequest, QueryResponse<EncryptionRecoveryResponse>>
{

}
