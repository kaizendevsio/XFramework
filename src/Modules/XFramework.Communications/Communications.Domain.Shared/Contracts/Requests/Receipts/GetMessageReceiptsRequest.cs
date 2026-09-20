namespace Communications.Domain.Shared.Contracts.Requests.Receipts;

/// <summary>Per-member delivery and read times for one message. Only the message's own sender may ask:
/// the counts on the message projection are deliberately coarse, and a per-person timeline is not.</summary>
[MemoryPackable]
public partial record GetMessageReceiptsRequest : RequestBase,
    IQuery<QueryResponse<GetMessageReceiptsResponse>>,
    IBoltRequest<GetMessageReceiptsRequest, QueryResponse<GetMessageReceiptsResponse>>
{
    public Guid ThreadId { get; set; }
    public Guid MessageId { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; } = 100;
}
