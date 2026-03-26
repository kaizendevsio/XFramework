using Wallets.Domain.Shared.Contracts.Responses;

namespace Wallets.Domain.Shared.Contracts.Requests;

using TRequest = GetWalletEventsRequest;
using TResponse = QueryResponse<List<WalletEventResponse>>;

[MemoryPackable]
public partial record GetWalletEventsRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid? WalletId { get; set; }
    public Guid? CredentialId { get; set; }
    public string? EventType { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; } = 50;
}
