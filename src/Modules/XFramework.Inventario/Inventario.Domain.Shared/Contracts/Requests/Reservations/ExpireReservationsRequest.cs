using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Reservations;

using TRequest = ExpireReservationsRequest;
using TResponse = CmdResponse;

[MemoryPackable]
public partial record ExpireReservationsRequest : RequestBase,
    ICommand<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public DateTime? ExpiresBefore { get; init; }
    public int MaxCount { get; init; } = 100;
}
