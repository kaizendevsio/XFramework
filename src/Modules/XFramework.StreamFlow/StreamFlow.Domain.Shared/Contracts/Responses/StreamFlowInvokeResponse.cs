using System.Net;
using MessagePack;

namespace StreamFlow.Domain.Shared.Contracts.Responses;

[MessagePackObject]
public class StreamFlowInvokeResponse
{
    [Key(0)]
    public HttpStatusCode HttpStatusCode { get; set; }
    
    [Key(1)]
    public string Message { get; set; }
    
    [Key(2)]
    public ReadOnlyMemory<byte> Response { get; set; }
}