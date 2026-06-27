namespace Communications.Domain.Shared.Contracts.Responses;

[MemoryPackable]
public partial record CreateThreadMessageResponse
{
    public Guid MessageId { get; set; }
}
