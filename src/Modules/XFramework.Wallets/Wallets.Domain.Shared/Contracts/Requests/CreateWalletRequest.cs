using Wallets.Domain.Shared.Contracts.Responses;

namespace Wallets.Domain.Shared.Contracts.Requests;

using TResponse = QueryResponse<WalletResponse>;

[MemoryPackable]
public partial record CreateWalletRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<CreateWalletRequest, TResponse>
{
    public Guid CredentialId { get; set; }
    public Guid WalletTypeId { get; set; }
    public decimal InitialBalance { get; set; }
}
