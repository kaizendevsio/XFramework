using Bolt.Domain.Shared.Contracts.Requests;
using XFramework.Domain.Shared.BusinessObjects;
using XFramework.Domain.Shared.Contracts.Requests;
using XFramework.Inventario.Domain.Shared.Enums;

namespace XFramework.Inventario.Domain.Shared.Contracts.Requests.Purchasing;

using TRequest = GetReceivingDocumentsRequest;
using TResponse = QueryResponse<List<ReceivingDocument>>;

[MemoryPackable]
public partial record GetReceivingDocumentsRequest : RequestBase,
    IQuery<TResponse>,
    IBoltRequest<TRequest, TResponse>
{
    public Guid? PurchaseOrderId { get; init; }
    public ReceivingDocumentStatus? Status { get; init; }
}
