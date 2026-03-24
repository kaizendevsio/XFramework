using System.Net;
using MessagePack;

namespace Bolt.Domain.Shared.Contracts.Responses;

[MessagePackObject]
public class BoltInvokeResponse
{
    [Key(0)]
    public HttpStatusCode HttpStatusCode { get; set; }
    
    [Key(1)]
    public string Message { get; set; }
    
    [Key(2)]
    public ReadOnlyMemory<byte> Response { get; set; }
}